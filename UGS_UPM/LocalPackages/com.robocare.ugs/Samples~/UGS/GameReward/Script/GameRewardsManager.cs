namespace RoboCare.UGS
{
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
    using Unity.Services.CloudCode;
    using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

    /// <summary>
    /// 누적 별 점수에 따른 단계별 보상을 관리하는 매니저
    /// </summary>
    /*
     * 사용 방법:
     * 1) Remote Config에 GAME_REWARDS(json) 키를 생성합니다.
            {
                "stage_rewards": [{
                    "id": "r_001",
                    "goal": 10,
                    "reward": 5
                }, {
                    "id": "r_002",
                    "goal": 20,
                    "reward": 5
                }]
            }
     * 2) 로그인 완료 후 questBtn 클릭 시 InitializeSequence로 보상 정의를 로드합니다.
     * 3) GameRewardsPanel/버튼/UI 참조를 인스펙터에 연결합니다.
     */
    public class GameRewardsManager : MonoBehaviour
    {
        public static GameRewardsManager Instance { get; private set; }

        #region [Fields - UI Components]
        [Header("UI Components")]
        [SerializeField] private GameRewardsPanel rewardsPanel;
        [SerializeField] private Button questBtn; // 보상 창 열기 버튼
        [SerializeField] private Button closeBtn; // 보상 창 닫기 버튼
        private bool tryInit = false;
        #endregion

        #region [Unity Lifecycle]
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
        }

        private void Start()
        {
            // 버튼 이벤트 바인딩
            questBtn.onClick.AddListener(() =>
            {
                if (tryInit == false) InitializeSequence();
                OpenPanel(true);
            });
            closeBtn.onClick.AddListener(() => OpenPanel(false));

            // 초기 상태는 패널 닫기
            OpenPanel(false);
        }
        #endregion

        #region [Initialization]
        /// <summary>
        /// 서비스 초기화 및 데이터 로드 시퀀스
        /// </summary>
        private async void InitializeSequence()
        {
            tryInit = true;
            await InitializeRemoteConfig();
            LoadRewardsDefinitions();
            UpdateCurrentRewardState();
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

                // Remote Config 데이터 패치
                await RemoteConfigService.Instance.FetchConfigsAsync(new userAttributes(), new appAttributes());
                LogApi.Log("[Rewards] Remote Config Fetch Completed");
            }
            catch (Exception e)
            {
                LogApi.LogError($"[Rewards] Remote Config Initialization Failed: {e.Message}");
            }
        }
        #endregion

        #region [Reward Settings]
        /// <summary>
        /// Remote Config에서 보상 정의 정보를 가져와 패널에 할당
        /// </summary>
        private void LoadRewardsDefinitions()
        {
            string json = RemoteConfigService.Instance.appConfig.GetJson("GAME_REWARDS");

            if (string.IsNullOrEmpty(json))
            {
                LogApi.LogError("[Rewards] 'GAME_REWARDS' definition not found in Remote Config");
                return;
            }

            var definitions = JsonConvert.DeserializeObject<RewardDefinitions>(json);

            if (definitions?.stage_rewards == null || definitions.stage_rewards.Count == 0)
            {
                LogApi.LogError("[Rewards] Failed to parse rewards or list is empty");
                return;
            }

            LogApi.Log($"[Rewards] Loaded {definitions.stage_rewards.Count} reward stages from server.");
            rewardsPanel.UpdateRewardLog(definitions);
        }
        #endregion

        #region [Claim & Persistence]
        /// <summary>
        /// 보상을 수령하고 PlayerPrefs에 즉시 저장
        /// </summary>
        public void ClaimRewardDirectlyToPrefs(string rewardId)
        {
            // 1. 기존 데이터 로드
            string rewardsJson = PlayerPrefs.GetString("CompleteGameRewards", "[]");
            List<string> rewardList = JsonConvert.DeserializeObject<List<string>>(rewardsJson);

            // 2. 중복 확인
            if (rewardList.Contains(rewardId))
            {
                LogApi.LogWarning($"[Rewards] Already claimed reward: {rewardId}");
                return;
            }

            // 3. 데이터 업데이트
            rewardList.Add(rewardId);

            // 4. PlayerPrefs 저장
            string updatedJson = JsonConvert.SerializeObject(rewardList);
            PlayerPrefs.SetString("CompleteGameRewards", updatedJson);
            PlayerPrefs.Save();

            LogApi.Log($"[Rewards] Successfully claimed and saved reward: {rewardId}");

            // 6. UI 상태 갱신
            UpdateCurrentRewardState();
        }

        /// <summary>
        /// 현재까지 획득한 전체 별 개수를 계산하여 UI 갱신
        /// </summary>
        public void UpdateCurrentRewardState()
        {
            int openLevel = PlayerPrefs.GetInt("OpenLevel", 1);
            int totalStarCount = 0;

            for (int i = 1; i <= openLevel; i++)
            {
                string levelKey = string.Format("Level.{0:000}.StarsCount", i);
                totalStarCount += PlayerPrefs.GetInt(levelKey, 0);
            }

            LogApi.Log($"[Rewards] Current Total Stars: {totalStarCount}");
            rewardsPanel.UpdateRewardUI(totalStarCount);
        }
        #endregion

        #region [UI Control]
        private void OpenPanel(bool isActive)
        {
            if (rewardsPanel != null)
            {
                rewardsPanel.gameObject.SetActive(isActive);
            }
        }
        #endregion
        #region cloud code
    private const string CompleteGameRewardEndpoint = "CompleteGameReward";
    private static bool IsValidRequest(CompleteGameRewardRequest request)
    {
        return request != null && !string.IsNullOrWhiteSpace(request.REWARD_ID);
    }

    public event Action<CompleteGameRewardResponse> OnCompleteGameRewardCompleted;
        public async Task<CompleteGameRewardResponse> CompleteGameRewardAsync(CompleteGameRewardRequest request)
    {
        if (!IsValidRequest(request))
        {
            var invalidResponse = BuildCompleteGameRewardErrorResponse("INVALID_PARAMS");
            NotifyCompleteGameRewardResult(invalidResponse);
            return invalidResponse;
        }

        var parameters = new Dictionary<string, object>
        {
            { "rewardId", request.REWARD_ID }
        };

        try
        {
            CompleteGameRewardResponse response =
                await CloudCodeService.Instance.CallEndpointAsync<CompleteGameRewardResponse>(
                    CompleteGameRewardEndpoint,
                    parameters);

            if (response == null)
            {
                response = BuildCompleteGameRewardErrorResponse("EMPTY_RESPONSE");
            }

            NotifyCompleteGameRewardResult(response);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerEventManager] CompleteGameReward failed: {e.Message}");
            var errorResponse = BuildCompleteGameRewardErrorResponse("CLOUD_CODE_ERROR");
            NotifyCompleteGameRewardResult(errorResponse);
            return errorResponse;
        }
    }

    public async void CompleteGameReward(
        CompleteGameRewardRequest request = null,
        Action<CompleteGameRewardResponse> callback = null)
    {
        CompleteGameRewardResponse response = await CompleteGameRewardAsync(request);
        callback?.Invoke(response);
    }

    private void NotifyCompleteGameRewardResult(CompleteGameRewardResponse response)
    {
        OnCompleteGameRewardCompleted?.Invoke(response);
    }
    
    private static CompleteGameRewardResponse BuildCompleteGameRewardErrorResponse(string errorCode)
    {
        return new CompleteGameRewardResponse
        {
            success = false,
            errorCode = errorCode,
            rewardId = string.Empty,
            reward = 0,
            currentMoney = 0L,
            updated = false,
            message = errorCode
        };
    }
    
    #endregion
}

#region [Data Models]
[Serializable]
public class RewardDefinitions
{
    public List<RewardData> stage_rewards;
}

[Serializable]
public class RewardData 
{
    public string id;     // 보상 고유 ID (예: "stars_50")
    public int goal;      // 목표 별 개수
    public int reward;    // 지급할 코인/아이템 양
}

#endregion
}
