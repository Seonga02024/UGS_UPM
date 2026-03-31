using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace RoboCare.UGS
{
    // 빈 오브젝트 만들어서 붙이기 
public class LoginManager : MonoBehaviour
    {
        public static LoginManager Instance { get; private set; }

        //[SerializeField] private LoginSuccessPanel loginSuccessPanel;
        //[SerializeField] private LoginTokenReader loginTokenReader;
        public bool IsLoggedIn { get; private set; }
        public string PlayerId => AuthenticationService.Instance.PlayerId;
        public event Action LoginCompleted;
        private bool _isInitialized;
        private bool _isLoggingIn = false;
        private string _userId = "";
        private string _robotId = "";
        private string _userName = "";

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

            // if (loginSuccessPanel != null)
            // {
            //     loginSuccessPanel.FinishAppLogin += HandleLoginSuccessPanelFinished;
            // }

            HandleLoginSuccessPanelFinished();
        }

        private void HandleLoginSuccessPanelFinished()
        {
            _ = LoginCloudAsync();
        }

        public async Task LoginCloudAsync()
        {
            await Task.Delay(3000); // 밀리초
            Debug.Log($"[LoginService] LoginCloudAsync Loading...");
            if (_isLoggingIn)
            {
                return;
            }

            _isLoggingIn = true;

            try
            {
                await EnsureServicesInitializedAsync();

                if (ShouldUseCamiLogin())
                {
                    await LoginWithCamiAsync();
                }
                else
                {
                    await LoginAnonymouslyAsync();
                }

                IsLoggedIn = true;
                LoginCompleted?.Invoke();
                Debug.LogError($"[LoginService] LoginCompleted Invoke");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LoginService] Login failed: {exception.Message}");
            }
            finally
            {
                _isLoggingIn = false;
            }
        }

        private bool ShouldUseCamiLogin()
        {
            return false;
            //return loginTokenReader != null && loginTokenReader.currentPlatform == PlatformType.CAMI;
        }

        private async Task EnsureServicesInitializedAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await UnityServices.InitializeAsync();
            _isInitialized = true;
        }

        private async Task LoginWithCamiAsync()
        {
            _robotId = "Password123!" + PlayerPrefs.GetString("robotid");
            _userId = "testuser" + PlayerPrefs.GetString("user");
            _userName = PlayerPrefs.GetString("username");

            try
            {
                await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(_userId, _robotId);
                Debug.Log($"[LoginService] Login success: _userId : {_userId} / _robotId : {_robotId} / _userName : {_userName}");
            }
            catch (AuthenticationException)
            {
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(_userId, _robotId);
            }
        }

        private static async Task LoginAnonymouslyAsync()
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("[LoginService] Login success: Anonymous login");
        }
    }
}
