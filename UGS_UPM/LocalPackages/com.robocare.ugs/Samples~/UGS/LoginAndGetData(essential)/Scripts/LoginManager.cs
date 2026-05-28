using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace RoboCare.UGS
{
    /*
     * 사용 방법:
     * 1) 첫 씬에 LoginManager 오브젝트를 배치합니다(싱글턴, DontDestroyOnLoad).
     * 2) Awake에서 로그인(LoginCloudAsync)을 시작하고 완료 시 LoginCompleted 이벤트를 발행합니다.
     * 3) 다른 매니저는 LoginCompleted를 구독해 후속 초기화를 시작합니다.
     */
    // 빈 오브젝트 만들어서 붙이기 
public class LoginManager : MonoBehaviour
    {
        public static LoginManager Instance { get; private set; }

        [SerializeField] private LoginSuccessPanel loginSuccessPanel;
        [SerializeField] private LoginTokenReader loginTokenReader;
        public bool IsLoggedIn { get; private set; }
        public bool IsLoggingIn => _isLoggingIn;
        public string PlayerId => AuthenticationService.Instance.PlayerId;
        public event Action LoginCompleted;
        public event Action<string> LoginFailed;
        private const int InitialLoginDelayMs = 3000;
        private const int LoginOperationTimeoutMs = 12000;
#if UNITY_IOS && !UNITY_EDITOR
        private const int IOSInitialLoginDelayMs = 0;
        private const int IOSLoginOperationTimeoutMs = 5000;
        private const int IOSLoginTotalTimeoutMs = 11000;
#endif
        private const int ServicesInitializationPollMs = 250;
        private bool _isInitialized;
        private bool _isLoggingIn = false;
        private string _userId = "";
        private string _robotId = "";
        private string _userName = "";
        private string _id = "";
        private string _password = "";

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
                return;
            }

            if (loginSuccessPanel != null)
            {
                loginSuccessPanel.FinishAppLogin += HandleLoginSuccessPanelFinished;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (loginSuccessPanel != null)
            {
                loginSuccessPanel.FinishAppLogin -= HandleLoginSuccessPanelFinished;
            }
        }

        private void HandleLoginSuccessPanelFinished()
        {
            _ = LoginCloudAsync();
        }

        public async Task LoginCloudAsync(bool skipInitialDelay = false)
        {
            if (_isLoggingIn)
            {
                LogApi.LogWarning("[LoginService] Login already in progress.");
                return;
            }

            _isLoggingIn = true;

            try
            {
                var delayMs = skipInitialDelay ? 0 : GetInitialLoginDelayMs();
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs);
                }

                LogApi.Log($"[LoginService] LoginCloudAsync start. timeoutMs:{GetLoginTotalTimeoutMs()}");
                await WithTimeout(LoginCloudCoreAsync(), GetLoginTotalTimeoutMs(), "UGS login sequence");
            }
            catch (Exception exception)
            {
                IsLoggedIn = ReadAuthenticationSignedInState();
                LogApi.LogError($"[LoginService] Login failed: {exception.Message}");
                LoginFailed?.Invoke(exception.Message);
            }
            finally
            {
                _isLoggingIn = false;
            }
        }

        private async Task LoginCloudCoreAsync()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                throw new InvalidOperationException("No network interface reachable.");
            }

            await EnsureServicesInitializedAsync();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                LogApi.Log("[LoginService] Existing UGS session detected.");
            }
            else if (ShouldUseCamiLogin())
            {
                await LoginWithCamiAsync();
            }
            else
            {
                await LoginAnonymouslyAsync();
            }

            IsLoggedIn = true;
            LoginCompleted?.Invoke();
            LogApi.Log($"[LoginService] LoginCompleted Invoke");
        }

        private bool ShouldUseCamiLogin()
        {
            return loginTokenReader != null && loginTokenReader.currentPlatform == PlatformType.CAMI;
        }

        private async Task EnsureServicesInitializedAsync()
        {
            if (_isInitialized || UnityServices.State == ServicesInitializationState.Initialized)
            {
                _isInitialized = true;
                return;
            }

            if (UnityServices.State == ServicesInitializationState.Initializing)
            {
                await WaitForServicesInitializationAsync();
                _isInitialized = true;
                return;
            }

            LogApi.Log($"[LoginService] Unity Services initialize start. timeoutMs:{GetLoginOperationTimeoutMs()}");
            await WithTimeout(UnityServices.InitializeAsync(), GetLoginOperationTimeoutMs(), "Unity Services initialize");
            LogApi.Log("[LoginService] Unity Services initialize completed.");
            _isInitialized = true;
        }

        private static async Task WaitForServicesInitializationAsync()
        {
            var remainingMs = GetLoginOperationTimeoutMs();
            while (remainingMs > 0)
            {
                if (UnityServices.State == ServicesInitializationState.Initialized)
                {
                    return;
                }

                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    break;
                }

                await Task.Delay(ServicesInitializationPollMs);
                remainingMs -= ServicesInitializationPollMs;
            }

            throw new TimeoutException("Unity Services initialize timed out while another initialization was in progress.");
        }

        private async Task LoginWithCamiAsync()
        {
            _robotId = "Password123!" + PlayerPrefs.GetString("robotid");
            _userId = "testuser" + PlayerPrefs.GetString("user");
            _userName = PlayerPrefs.GetString("username");
            (_id, _password) = UgsCredentialGenerator.CreateCredentials(PlayerPrefs.GetString("user"));

            try
            {
                Debug.Log($"[LoginService] Login success: _id : {_id} / _password : {_password}");
                await WithTimeout(AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(_id, _password), GetLoginOperationTimeoutMs(), "UGS CAMI sign-up");
            }
            catch (AuthenticationException)
            {
                await WithTimeout(AuthenticationService.Instance.SignInWithUsernamePasswordAsync(_id, _password), GetLoginOperationTimeoutMs(), "UGS CAMI sign-in");
            }
        }

        private static async Task LoginAnonymouslyAsync()
        {
            LogApi.Log($"[LoginService] Anonymous sign-in start. timeoutMs:{GetLoginOperationTimeoutMs()}");
            await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync(), GetLoginOperationTimeoutMs(), "UGS anonymous login");
            LogApi.Log("[LoginService] Login success: Anonymous login");
        }

        private static int GetInitialLoginDelayMs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return IOSInitialLoginDelayMs;
#else
            return InitialLoginDelayMs;
#endif
        }

        private static int GetLoginOperationTimeoutMs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return IOSLoginOperationTimeoutMs;
#else
            return LoginOperationTimeoutMs;
#endif
        }

        private static int GetLoginTotalTimeoutMs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return IOSLoginTotalTimeoutMs;
#else
            return InitialLoginDelayMs + (LoginOperationTimeoutMs * 2);
#endif
        }

        private static bool ReadAuthenticationSignedInState()
        {
            try
            {
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch
            {
                return false;
            }
        }

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
    }
}
