using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    /*
     * 사용 방법:
     * 1) backPanel(어두운 배경)과 netCheckPanel(재시도 팝업)을 인스펙터에 연결합니다.
     * 2) 로그인/데이터 로드 실패 이벤트에서 네트워크 경고 UI를 표시합니다.
     * 3) iOS 비행기 모드는 FinishAppLogin 직후 즉시 감지해 실패 이벤트 대기 없이 표시합니다.
     */
    public class NetCheckManager : MonoBehaviour
    {
        [Tooltip("Full-screen dim/background panel. Prefab object: updatePanel.")]
        [SerializeField] private GameObject backPanel;
        [Tooltip("Retry popup panel. Prefab object: backgroundImg.")]
        [SerializeField] private GameObject netCheckPanel;
        [SerializeField] private Button quitBtn;
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text buttonText;
        [SerializeField] private LoginManager loginManager;
        [SerializeField] private PlayerDataManager playerDataManager;

        public bool IsInternetAvailable { get; private set; }
        private const string CheckUrl = "https://clients3.google.com/generate_204";
        private const int TimeoutSec = 3;
        [SerializeField] private LoginSuccessPanel loginSuccessPanel;
        private Coroutine _internetCheckRoutine;
        private LoginManager _subscribedLoginManager;
        private PlayerDataManager _subscribedPlayerDataManager;
        public bool isCheckPlayerData = true;

        private void OnEnable()
        {
            backPanel.SetActive(false);
            netCheckPanel.SetActive(false);
            SubscribeFailureEvents();
            if (loginSuccessPanel != null)
            {
                loginSuccessPanel.FinishAppLogin += HandleLoginSuccessPanelFinished;
            }
        }

        private void OnDisable()
        {
            if (loginSuccessPanel != null)
            {
                loginSuccessPanel.FinishAppLogin -= HandleLoginSuccessPanelFinished;
            }

            UnsubscribeFailureEvents();
        }

        private void HandleLoginSuccessPanelFinished()
        {
            SubscribeFailureEvents();
            backPanel.SetActive(false);
            netCheckPanel.SetActive(false);

            quitBtn.onClick.RemoveAllListeners();
#if UNITY_IOS
            // Apple HIG: iOS 앱은 스스로 종료하지 않는다 → 재시도 버튼으로 동작
            mainText.text = "인터넷 연결이 필요합니다";
            detailText.text = "Wi-Fi 또는 셀룰러 데이터 연결을 \n 확인한 후 다시 시도해 주세요.";
            buttonText.text = "다시 시도";
            quitBtn.onClick.AddListener(RetryInternetCheck);

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                IsInternetAvailable = false;
                LogApi.LogWarning("[NetCheckManager] iOS network interface not reachable after FinishAppLogin. show retry panel.");
                ShowNetworkErrorPanel();
                return;
            }
#else
            mainText.text = "인터넷 연결 상태를 \n 확인해 주세요.";
            detailText.text = "인터넷이 연결되지 않아 \n 게임을 실행할 수 없습니다.";
            buttonText.text = "종료하기";
            quitBtn.onClick.AddListener(() => Application.Quit());
#endif
        }

#if UNITY_IOS
        private void RetryInternetCheck()
        {
            StartInternetCheck(true);
        }
#endif

        private void StartInternetCheck(bool retryLoginOnSuccess)
        {
            if (_internetCheckRoutine != null)
            {
                StopCoroutine(_internetCheckRoutine);
            }

            _internetCheckRoutine = StartCoroutine(CheckInternetNow(retryLoginOnSuccess));
        }

        private IEnumerator CheckInternetNow(bool retryLoginOnSuccess)
        {
            var loginTokenReader = LoginTokenReader.Instance;
            if (loginTokenReader != null &&
                (loginTokenReader.currentPlatform == PlatformType.BOMI1 || loginTokenReader.currentPlatform == PlatformType.BOMI2))
            {
                backPanel.SetActive(false);
                netCheckPanel.SetActive(false);
                _internetCheckRoutine = null;
                yield break;
            }

            // 1) 로컬 네트워크 연결 유무
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                IsInternetAvailable = false;
                ShowNetworkErrorPanel();
                LogApi.Log("No network interface reachable.");
                _internetCheckRoutine = null;
                yield break;
            }

            // 2) 실제 외부 접속 확인
            using var req = UnityWebRequest.Get(CheckUrl);
            req.timeout = TimeoutSec;
            yield return req.SendWebRequest();

            IsInternetAvailable =
                req.result == UnityWebRequest.Result.Success &&
                req.responseCode == 204;

            if (IsInternetAvailable)
            {
                netCheckPanel.SetActive(false);
                backPanel.SetActive(false);

                if (retryLoginOnSuccess)
                {
                    RetryLoginSequence();
                }
            }
            else
            {
                ShowNetworkErrorPanel();
            }

            LogApi.Log($"Internet available: {IsInternetAvailable}");
            _internetCheckRoutine = null;
        }

        private void HandleLoginFailed(string message)
        {
            LogApi.LogWarning("[NetCheckManager] Login failed. show retry panel. " + message);
            ShowNetworkErrorPanel();
        }

        private void HandlePlayerDataFailed(string message)
        {
            LogApi.LogWarning("[NetCheckManager] Player data load failed. show retry panel. " + message);
            ShowNetworkErrorPanel();
        }

        private void RetryLoginSequence()
        {
            ShowLoadingPanelsForRetry();
            SubscribeFailureEvents();

            if (loginManager == null)
            {
                LogApi.LogWarning("[NetCheckManager] LoginManager not found for retry.");
                ShowNetworkErrorPanel();
                return;
            }

            if (!loginManager.IsLoggedIn)
            {
                _ = loginManager.LoginCloudAsync(true);
                return;
            }

            if (isCheckPlayerData && playerDataManager != null && !playerDataManager.IsPlayerDataLoaded)
            {
                _ = playerDataManager.RetryPostLoginSequenceAsync();
                return;
            }

            HideLoadingPanelsForNetworkError();
        }

        private void ShowNetworkErrorPanel()
        {
            HideLoadingPanelsForNetworkError();
            backPanel.SetActive(true);
            netCheckPanel.SetActive(true);
            transform.SetAsLastSibling();
            backPanel.transform.SetAsLastSibling();
            netCheckPanel.transform.SetAsLastSibling();
        }

        private static void HideLoadingPanelsForNetworkError()
        {
            var loadingPanels = FindObjectsByType<LoadingLoginAndGetDataUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var loadingPanel in loadingPanels)
            {
                loadingPanel.HideForNetworkError();
            }
        }

        private static void ShowLoadingPanelsForRetry()
        {
            var loadingPanels = FindObjectsByType<LoadingLoginAndGetDataUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var loadingPanel in loadingPanels)
            {
                loadingPanel.ShowForRetry();
            }
        }

        private void SubscribeFailureEvents()
        {
            ResolveManagers();

            if (_subscribedLoginManager != loginManager)
            {
                if (_subscribedLoginManager != null)
                {
                    _subscribedLoginManager.LoginFailed -= HandleLoginFailed;
                }

                _subscribedLoginManager = loginManager;
                if (_subscribedLoginManager != null)
                {
                    _subscribedLoginManager.LoginFailed += HandleLoginFailed;
                }
            }

            if (isCheckPlayerData && _subscribedPlayerDataManager != playerDataManager)
            {
                if (_subscribedPlayerDataManager != null)
                {
                    _subscribedPlayerDataManager.GetDataFailed -= HandlePlayerDataFailed;
                }

                _subscribedPlayerDataManager = playerDataManager;
                if (_subscribedPlayerDataManager != null)
                {
                    _subscribedPlayerDataManager.GetDataFailed += HandlePlayerDataFailed;
                }
            }
        }

        private void UnsubscribeFailureEvents()
        {
            if (_subscribedLoginManager != null)
            {
                _subscribedLoginManager.LoginFailed -= HandleLoginFailed;
                _subscribedLoginManager = null;
            }

            if (isCheckPlayerData && _subscribedPlayerDataManager != null)
            {
                _subscribedPlayerDataManager.GetDataFailed -= HandlePlayerDataFailed;
                _subscribedPlayerDataManager = null;
            }
        }

        private void ResolveManagers()
        {
            if (loginManager == null)
            {
                loginManager = LoginManager.Instance;
            }

            if (isCheckPlayerData && playerDataManager == null)
            {
                playerDataManager = PlayerDataManager.Instance;
            }
        }
    }
}
