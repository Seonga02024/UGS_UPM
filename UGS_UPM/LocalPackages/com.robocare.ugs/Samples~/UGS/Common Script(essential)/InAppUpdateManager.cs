using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    /*
     * 사용 방법:
     * 1) Remote Config에 min_required_bundle_code_android, min_required_version_android,
     *    force_update_message_ko, store_url_android, can_pass_update 키를 생성합니다.
     * 2) loginService(LoginManager)의 LoginCompleted 이벤트를 받으면 업데이트 체크를 시작합니다.
     * 3) laterButton 허용 시 UpdateCompleted 이벤트를 발행해 다음 매니저 흐름을 이어갑니다.
     */
    // Canvas InAppUpdateUI Prefab 에 붙이기 
   public class InAppUpdateManager : MonoBehaviour
    {
        private const string BomphagoIOSStoreUrl = "https://apps.apple.com/app/id6764503478";

        [SerializeField] private LoginManager loginService;

        [Header("Remote Config Keys")]
        [SerializeField] private string minimumBundleCodeKey = "min_required_bundle_code_android";
        [SerializeField] private string minimumVersionKey = "min_required_version_android";
        [SerializeField] private string forceUpdateMessageKey = "force_update_message_ko";
        [SerializeField] private string forceUpdateDetailMessageKey = "force_update_message_detail_ko";
        [SerializeField] private string storeUrlKey = "store_url_android";
        [SerializeField] private string canPassUpdate = "can_pass_update";

        [Header("Fallback Values")]
        [SerializeField] private int fallbackMinimumBundleCode = 0;
        [SerializeField] private string fallbackMinimumVersion = "1.0.0";
        [SerializeField] private string fallbackMessage = "A new update is required.";
        [SerializeField] private string fallbackStoreUrl = "market://details?id=com.robocare.bomphago";
        [SerializeField] private bool fallbackCanPassUpdate = true;
        [SerializeField] private string fallbackDetailMessage = "저희 앱을 이용해주시는 사용자님께 \n진심으로 감사드립니다. 더 나은 서비스를 \n위하여 앱을 업데이트 하시길 바랍니다. \n감사합니다.";

        [Header("Optional UI")]
        [SerializeField] private Button cheatButton;
        [SerializeField] private GameObject cheatVersion;
        [SerializeField] private GameObject backImg;
        [SerializeField] private GameObject updatePanel;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text detailMessageText;
        [SerializeField] private TMP_Text MinimumBundleCodeText;
        [SerializeField] private TMP_Text CurrentBundleCodeText;
        [SerializeField] private TMP_Text MinimumVersionText;
        [SerializeField] private TMP_Text CurrentVersionText;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button laterButton;
        private bool _canPassUpdate;
        public event Action UpdateCompleted;
        private int cheatNum = 0;
#if UNITY_IOS && !UNITY_EDITOR
        private const int RemoteConfigOperationTimeoutMs = 5000;
#endif

        private void Start()
        {
            cheatVersion.SetActive(false);
            updatePanel.SetActive(false);
            if(LoginTokenReader.IsFirstAppOpen == false) backImg.SetActive(false);
            if (loginService != null)
            {
                loginService.LoginCompleted += HandleLoginCompleted;
            }

            if (laterButton != null)
            {
                laterButton.onClick.AddListener(() =>
                {
                    //PlayClickSound();
                    if (updatePanel != null)
                    {
                        backImg.SetActive(false);
                        updatePanel.SetActive(false);
                        UpdateCompleted?.Invoke();
                    }
                });
            }

            if (cheatButton != null)
            {
                cheatButton.onClick.AddListener(() =>
                {
                    //PlayClickSound();
                    cheatNum++;
                    if (cheatNum > 5)
                    {
                        cheatVersion.SetActive(true);
                    }
                });
            }
        }

        private void HandleLoginCompleted()
        {
            if (LoginTokenReader.Instance.currentPlatform == PlatformType.BOMI1 || LoginTokenReader.Instance.currentPlatform == PlatformType.BOMI2)
            {
                backImg.SetActive(false);
                cheatVersion.SetActive(false);
                updatePanel.SetActive(false);
                return;
            }
            backImg.SetActive(true);
            //#if UNITY_ANDROID && !UNITY_EDITOR
            _ = RunPostLoginDataSyncAsync();
            //#endif
        }

        private async Task RunPostLoginDataSyncAsync()
        {
            await CheckAndHandleUpdateAsync();
        }

        private async Task CheckAndHandleUpdateAsync()
        {
            var minimumBundleCode = fallbackMinimumBundleCode;
            var minimumVersionCode = fallbackMinimumVersion;
            var updateMessage = fallbackMessage;
            var storeUrl = fallbackStoreUrl;
            var updateDetailMessage = fallbackDetailMessage;

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                Debug.Log($"[InAppUpdate] UGS ready check start. timeoutMs:{RemoteConfigOperationTimeoutMs}");
                await WithTimeout(EnsureUgsReadyAsync(), RemoteConfigOperationTimeoutMs, "UGS ready for Remote Config");
                Debug.Log($"[InAppUpdate] Remote Config fetch start. timeoutMs:{RemoteConfigOperationTimeoutMs}");
                await WithTimeout(RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes()), RemoteConfigOperationTimeoutMs, "Remote Config fetch");
#else
                await EnsureUgsReadyAsync();
                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
#endif

                minimumBundleCode = (int)RemoteConfigService.Instance.appConfig.GetInt(minimumBundleCodeKey, fallbackMinimumBundleCode);
                minimumVersionCode = RemoteConfigService.Instance.appConfig.GetString(minimumVersionKey, fallbackMinimumVersion);
                updateMessage = RemoteConfigService.Instance.appConfig.GetString(forceUpdateMessageKey, fallbackMessage);
                updateDetailMessage = RemoteConfigService.Instance.appConfig.GetString(forceUpdateDetailMessageKey, fallbackDetailMessage);
                storeUrl = RemoteConfigService.Instance.appConfig.GetString(storeUrlKey, fallbackStoreUrl);
                _canPassUpdate = RemoteConfigService.Instance.appConfig.GetBool(canPassUpdate, fallbackCanPassUpdate);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[InAppUpdate] Remote Config fetch failed. fallback values will be used. " + e.Message);
            }

            var installedBundleCode = GetInstalledBundleCode();
            string currentVersion = Application.version;
            UpdateBundleCodeTexts(minimumBundleCode, installedBundleCode);
            UpdateVersionTexts(minimumVersionCode, currentVersion);
            Debug.Log(string.Format(
                "[InAppUpdate] platform={0}, installedBundleCode={1}, minimumBundleCode={2}",
                Application.platform,
                installedBundleCode,
                minimumBundleCode));

            if (installedBundleCode < minimumBundleCode)
            {
                ShowUpdateUi(updateMessage, updateDetailMessage, storeUrl);
            }
            else
            {
                updatePanel.SetActive(false);
                backImg.SetActive(false);
            }
        }

        private async Task EnsureUgsReadyAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
#if UNITY_IOS && !UNITY_EDITOR
                await WithTimeout(UnityServices.InitializeAsync(), RemoteConfigOperationTimeoutMs, "Unity Services initialize for Remote Config");
#else
                await UnityServices.InitializeAsync();
#endif
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
#if UNITY_IOS && !UNITY_EDITOR
                await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync(), RemoteConfigOperationTimeoutMs, "UGS anonymous sign-in for Remote Config");
#else
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
#endif
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static async Task WithTimeout(Task task, int timeoutMs, string operationName)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completedTask != task)
            {
                ObserveFault(task);
                throw new TimeoutException($"{operationName} timed out after {timeoutMs}ms.");
            }

            await task;
        }

        private static void ObserveFault(Task task)
        {
            task.ContinueWith(t => { var _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
#endif

        private void ShowUpdateUi(string message, string detailMessage, string storeUrl)
        {
            if (updatePanel != null)
            {
                updatePanel.SetActive(true);
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            if (detailMessageText != null)
            {
                detailMessageText.text = detailMessage;
            }

            if (updateButton != null)
            {
                updateButton.onClick.RemoveAllListeners();
                updateButton.onClick.AddListener(() => OpenStore(storeUrl));
            }

            if (laterButton != null)
            {
                laterButton.gameObject.SetActive(_canPassUpdate);
            }
        }

        private void OpenStore(string configuredStoreUrl)
        {
            var url = ResolveStoreUrl(configuredStoreUrl);
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[InAppUpdate] Store URL is empty.");
                return;
            }

            Application.OpenURL(url);
        }

        private string ResolveStoreUrl(string configuredStoreUrl)
        {
            var url = string.IsNullOrEmpty(configuredStoreUrl) ? fallbackStoreUrl : configuredStoreUrl;
#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrEmpty(url) || IsAndroidStoreUrl(url))
                return BomphagoIOSStoreUrl;
#endif
            return url;
        }

        private static bool IsAndroidStoreUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            return url.StartsWith("market://", StringComparison.OrdinalIgnoreCase)
                   || url.IndexOf("play.google.com/store/apps/details", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateBundleCodeTexts(int minimumBundleCode, int currentBundleCode)
        {
            if (MinimumBundleCodeText != null)
            {
                MinimumBundleCodeText.text = "요구 번들 버전 : " + minimumBundleCode.ToString();

            }

            if (CurrentBundleCodeText != null)
            {
                CurrentBundleCodeText.text = "현재 번들 버전 : " + currentBundleCode.ToString();
            }
        }

        private void UpdateVersionTexts(string minimumVersion, string currentVersion)
        {
            if (minimumVersion != null)
            {
                MinimumVersionText.text = "요구 버전 : " + minimumVersion.ToString();

            }

            if (currentVersion != null)
            {
                CurrentVersionText.text = "현재 버전 : " + currentVersion.ToString();
            }
        }

        private int GetInstalledBundleCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
            {
                var packageName = currentActivity.Call<string>("getPackageName");
                using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0))
                {
                    return packageInfo.Get<int>("versionCode");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[InAppUpdate] versionCode read failed: " + e.Message);
            return 0;
        }
#elif UNITY_IOS && !UNITY_EDITOR
            var buildNumber = global::BuildInfo.GetIOSBuildNumber();
            if (int.TryParse(buildNumber, out var parsed))
            {
                return parsed;
            }

            Debug.LogWarning("[InAppUpdate] iOS CFBundleVersion parse failed: " + buildNumber);
            return 0;
#else
            return 0;
#endif
        }

        private struct UserAttributes
        {
        }

        private struct AppAttributes
        {
        }
        
        // private static void PlayClickSound()
        // {
        //     if (AudioManager.Instance != null)
        //     {
        //         AudioManager.Instance?.PlayButtonSound();
        //     }
        // }
    }
}
