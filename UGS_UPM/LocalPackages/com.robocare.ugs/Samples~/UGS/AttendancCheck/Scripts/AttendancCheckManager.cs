using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    /*
     * 사용 방법:
     * 1) Remote Config에 ATTENDANCE_REWARDS(json) 키를 생성합니다.
        {
        "day_rewards": [{
            "id": "1",
            "reward": 100000
        }, {
            "id": "2",
            "reward": 100000
        }]
        }
     * 2) 로그인 완료 후 출석 버튼(attendancCheckBtn) 클릭 시 InitializeSequence가 실행됩니다.
     * 3) AttendancCheckPanel, 버튼, 날짜 텍스트를 인스펙터에 연결합니다.
     * 4) AttendanceStateKey 는 Cloud Save Player Data ATTENDANCE_STATE 에 생성되며 초기 파싱할 때 없다면 처음 출석하는 유저 상태로 전환
     * 5) Cloud code 'ClaimAttendanceReward' 를 통해서 출석 체크 했다는 이벤트를 보내고 서버에서 검증해 리워드 보상 및 AttendanceStateKey 업데이트 
     * 6) Cloud code 는 해당 폴더 안 ClaimAttendanceReward.js 참고 
     */
    public class AttendancCheckManager : MonoBehaviour
    {
        public static AttendancCheckManager Instance { get; private set; }

        private const string AttendanceRewardsKey = "ATTENDANCE_REWARDS";
        private const string AttendanceStateKey = "ATTENDANCE_STATE";
        private const int KstOffsetHours = 9;

        [Header("UI Components")]
        [SerializeField] private AttendancCheckPanel attendancCheckPanel;
        [SerializeField] private Button attendancCheckBtn;
        [SerializeField] private Button closeBtn;

        private bool tryInit;
        private bool isClaiming;
        private DayRewardDefinitions cachedDefinitions;
        private int currentClaimCount;
        private bool hasClaimedToday;
        public event Action<ClaimAttendanceRewardResponse> OnClaimAttendanceRewardCompleted;
        private const string ClaimAttendanceRewardEndpoint = "ClaimAttendanceReward";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            tryInit = false;
            isClaiming = false;
            currentClaimCount = 0;
            hasClaimedToday = false;
        }

        private void Start()
        {
            if (attendancCheckBtn != null)
            {
                attendancCheckBtn.onClick.AddListener(() =>
                {
                    if (!tryInit)
                    {
                        InitializeSequence();
                    }
                    else
                    {
                        _ = RefreshAttendanceStateUiAsync();
                    }

                    OpenPanel(true);
                });
            }

            if (closeBtn != null)
            {
                closeBtn.onClick.AddListener(() => OpenPanel(false));
            }

            OpenPanel(false);
        }

        private async void InitializeSequence()
        {
            tryInit = true;
            await InitializeRemoteConfig();
            LoadRewardsDefinitions();
            await RefreshAttendanceStateUiAsync();
        }

        private async Task InitializeRemoteConfig()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
            }
            catch (Exception e)
            {
                LogApi.LogError($"[Attendance] Remote Config init failed: {e.Message}");
            }
        }

        private void LoadRewardsDefinitions()
        {
            string json = RemoteConfigService.Instance.appConfig.GetJson(AttendanceRewardsKey);
            if (string.IsNullOrEmpty(json))
            {
                LogApi.LogError("[Attendance] ATTENDANCE_REWARDS not found in Remote Config.");
                return;
            }

            cachedDefinitions = JsonConvert.DeserializeObject<DayRewardDefinitions>(json);
            if (cachedDefinitions?.day_rewards == null || cachedDefinitions.day_rewards.Count == 0)
            {
                LogApi.LogError("[Attendance] Parsed rewards are empty.");
                return;
            }

            attendancCheckPanel?.UpdateRewardLog(cachedDefinitions);
        }

        public void TryClaimAttendance(string rewardId)
        {
            if (isClaiming)
            {
                return;
            }

            int expectedDay = currentClaimCount + 1;
            if (!string.Equals(rewardId, expectedDay.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            if (hasClaimedToday)
            {
                return;
            }

            isClaiming = true;
            // 서버로 출석체크 검증하는 코드 
            var req = new ClaimAttendanceRewardRequest();
            ClaimAttendanceReward(req, res =>
            {
                HandleClaimAttendanceCompleted(res);
                LogApi.Log(
                    $"[Callback] success={res.success}, currentMoney={res.currentMoney}, rewardDay={res.rewardDay}, reward={res.reward}, claimCount={res.claimCount}, monthKey={res.monthKey}, error={res.errorCode}");
            });
        }

        private async void HandleClaimAttendanceCompleted(ClaimAttendanceRewardResponse res)
        {
            isClaiming = false;

            if (res == null)
            {
                return;
            }

            if (res.success)
            {
                currentClaimCount = Mathf.Max(0, res.claimCount);
                hasClaimedToday = true;
                attendancCheckPanel?.ApplyClaimState(currentClaimCount, hasClaimedToday);
                return;
            }

            if (res.errorCode == "ALREADY_CLAIMED_TODAY")
            {
                hasClaimedToday = true;
                attendancCheckPanel?.ApplyClaimState(currentClaimCount, hasClaimedToday);
                return;
            }

            await RefreshAttendanceStateUiAsync();
        }

        private async Task RefreshAttendanceStateUiAsync()
        {
            try
            {
                AttendanceStateData state = await LoadAttendanceStateAsync();
                string currentMonthKey = GetCurrentMonthKeyKst();

                if (!string.Equals(state.monthKey, currentMonthKey, StringComparison.Ordinal))
                {
                    state.claimCount = 0;
                    state.lastClaimAt = string.Empty;
                }

                currentClaimCount = Mathf.Max(0, state.claimCount);
                hasClaimedToday = IsClaimedTodayKst(state.lastClaimAt);

                attendancCheckPanel?.ApplyClaimState(currentClaimCount, hasClaimedToday);
            }
            catch (Exception e)
            {
                LogApi.LogError($"[Attendance] Refresh state failed: {e.Message}");
            }
        }

        private static string GetCurrentMonthKeyKst()
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime kstNow = utcNow.AddHours(KstOffsetHours);
            return $"{kstNow:yyyy-MM}";
        }

        private static string GetDateKeyKst(DateTime utcTime)
        {
            DateTime kst = utcTime.AddHours(KstOffsetHours);
            return $"{kst:yyyy-MM-dd}";
        }

        private static bool IsClaimedTodayKst(string lastClaimAtIso)
        {
            if (string.IsNullOrWhiteSpace(lastClaimAtIso))
            {
                return false;
            }

            if (!DateTime.TryParse(lastClaimAtIso, out DateTime lastClaimUtc))
            {
                return false;
            }

            string todayKey = GetDateKeyKst(DateTime.UtcNow);
            string lastKey = GetDateKeyKst(lastClaimUtc.ToUniversalTime());
            return string.Equals(todayKey, lastKey, StringComparison.Ordinal);
        }

        private async Task<AttendanceStateData> LoadAttendanceStateAsync()
        {
            var keys = new HashSet<string> { AttendanceStateKey };
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

            if (!data.TryGetValue(AttendanceStateKey, out var item))
            {
                return new AttendanceStateData();
            }

            string json = item.Value.GetAsString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AttendanceStateData();
            }

            try
            {
                AttendanceStateData parsed = JsonConvert.DeserializeObject<AttendanceStateData>(json);
                return parsed ?? new AttendanceStateData();
            }
            catch
            {
                return new AttendanceStateData();
            }
        }

        private void OpenPanel(bool isActive)
        {
            if (attendancCheckPanel != null)
            {
                attendancCheckPanel.gameObject.SetActive(isActive);
            }
        }

        private struct UserAttributes { }
        private struct AppAttributes { }

        [Serializable]
        private class AttendanceStateData
        {
            public string monthKey;
            public string lastClaimAt;
            public int claimCount;
        }

        #region cloud code

        // 서버로 출석체크 검증하는 코드 
        public async void ClaimAttendanceReward(
            ClaimAttendanceRewardRequest request = null,
            Action<ClaimAttendanceRewardResponse> callback = null)
        {
            ClaimAttendanceRewardResponse response = await ClaimAttendanceRewardAsync(request);
            callback?.Invoke(response);
        }

        private void NotifyClaimAttendanceRewardResult(ClaimAttendanceRewardResponse response)
        {
            OnClaimAttendanceRewardCompleted?.Invoke(response);
        }

        public async Task<ClaimAttendanceRewardResponse> ClaimAttendanceRewardAsync(
        ClaimAttendanceRewardRequest request)
        {
            if (request == null)
            {
                request = new ClaimAttendanceRewardRequest();
            }

            if (!IsValidRequest(request))
            {
                var invalidResponse = BuildAttendanceErrorResponse("INVALID_PARAMS");
                NotifyClaimAttendanceRewardResult(invalidResponse);
                return invalidResponse;
            }

            var parameters = new Dictionary<string, object>();

            try
            {
                ClaimAttendanceRewardResponse response =
                    await CloudCodeService.Instance.CallEndpointAsync<ClaimAttendanceRewardResponse>(
                        ClaimAttendanceRewardEndpoint,
                        parameters);

                if (response == null)
                {
                    response = BuildAttendanceErrorResponse("EMPTY_RESPONSE");
                }

                NotifyClaimAttendanceRewardResult(response);
                return response;
            }
            catch (Exception e)
            {
                LogApi.LogError($"[ServerEventManager] ClaimAttendanceReward failed: {e.Message}");
                var errorResponse = BuildAttendanceErrorResponse("CLOUD_CODE_ERROR");
                NotifyClaimAttendanceRewardResult(errorResponse);
                return errorResponse;
            }
        }

        private static bool IsValidRequest(ClaimAttendanceRewardRequest request)
        {
            return request != null;
        }

        private static ClaimAttendanceRewardResponse BuildAttendanceErrorResponse(string errorCode)
        {
            return new ClaimAttendanceRewardResponse
            {
                success = false,
                errorCode = errorCode,
                currentMoney = 0,
                rewardDay = string.Empty,
                reward = 0,
                claimCount = 0,
                monthKey = string.Empty
            };
        }

        #endregion
    }

    [Serializable]
    public class DayRewardDefinitions
    {
        public List<DayRewardData> day_rewards;
    }

    [Serializable]
    public class DayRewardData
    {
        public string id;
        public int reward;
    }

    [Serializable]
    public class ClaimAttendanceRewardResponse
    {
        public bool success;
        public string errorCode;
        public long currentMoney;
        public string rewardDay;
        public int reward;
        public int claimCount;
        public string monthKey;
    }

    [Serializable]
    public class ClaimAttendanceRewardRequest
    {
    }
}
