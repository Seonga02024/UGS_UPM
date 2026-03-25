using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    public class AttendancCheckManager : MonoBehaviour
    {
        public static AttendancCheckManager Instance { get; private set; }

        #region [Fields - UI Components]
        [Header("UI Components")]
        [SerializeField] private AttendancCheckPanel attendancCheckPanel;
        [SerializeField] private Button attendancCheckBtn; // 보상 창 열기 버튼
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
            attendancCheckBtn.onClick.AddListener(() =>
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
                Debug.Log("[Rewards] Remote Config Fetch Completed");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Rewards] Remote Config Initialization Failed: {e.Message}");
            }
        }
        #endregion

        #region [Reward Settings]
        /// <summary>
        /// Remote Config에서 보상 정의 정보를 가져와 패널에 할당
        /// </summary>
        private void LoadRewardsDefinitions()
        {
            string json = RemoteConfigService.Instance.appConfig.GetJson("ATTENDANCE_REWARDS");

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[Rewards] 'GAME_REWARDS' definition not found in Remote Config");
                return;
            }

            var definitions = JsonConvert.DeserializeObject<DayRewardDefinitions>(json);

            if (definitions?.day_rewards == null || definitions.day_rewards.Count == 0)
            {
                Debug.LogError("[Rewards] Failed to parse rewards or list is empty");
                return;
            }

            Debug.Log($"[Rewards] Loaded {definitions.day_rewards.Count} reward stages from server.");
            attendancCheckPanel.UpdateRewardLog(definitions);
        }
        #endregion

        #region [UI Control]
        private void OpenPanel(bool isActive)
        {
            if (attendancCheckPanel != null)
            {
                attendancCheckPanel.gameObject.SetActive(isActive);
            }
        }
        #endregion
    }
    
    #region [Data Models]
    [Serializable]
    public class DayRewardDefinitions
    {
        public List<DayRewardData> day_rewards;
    }

    [Serializable]
    public class DayRewardData 
    {
        public string id;     // 보상 고유 ID 
        public int reward;    // 지급할 코인/아이템 양
    }

    #endregion
}
