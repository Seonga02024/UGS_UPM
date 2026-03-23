namespace RoboCare.UGS
{
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum PlatformType
{
    BOMI1 = 0,
    BOMI2 = 1,
    CAMI = 2,
    Others = 3,
}

public class LoginTokenReader : MonoBehaviour
{
    [Header("Loading Login")]
    [SerializeField] private LoginLoadingPanel loadingPanel;

    [Header("Login Success")]
    [SerializeField] private LoginSuccessPanel loginSuccessPanel;
    [SerializeField] private TMP_Text loginSuccessInfoText;

    [Header("Login Failed")]
    [SerializeField] private GameObject loginFailPanel;
    [SerializeField] private TMP_Text loginFailInfoText;
    [SerializeField] private Button goStoreBtn;
    [SerializeField] private Button guestBtn;
    [SerializeField] private Button camiBtn;

    [Header("Platform Selector")]
    [SerializeField] private PlatformSelectorPanel platformSelector;

    [Header("Settings")]
    [SerializeField] private TMP_Text tokenText;
    [SerializeField] private Button secretBtn2;

    [Header("Tos Panel")]
    [SerializeField] private TermsGate tosPanel;

    [Header("Blocker")]
    public GameObject blocker;

    [Header("Platform Colors")]
    public Color color_bomi1 = new Color(0.07f, 0.81f, 0.92f);
    public Color color_cami = new Color(0.94f, 0.54f, 0.84f);
    public Color color_others = new Color(0.72f, 0.40f, 0.78f);

    private readonly HashSet<string> _locks = new HashSet<string>();

    private string _userId = "";
    private string _userName = "";
    private string _robotId = "";
    private string _prjId = "";
    private string _prjName = "";

    private int robotTypeInt = 0;
    [NonSerialized] public PlatformType currentPlatform;

    private const string AppLaunchedKey = PrefKeys.AppLaunched;
    private const string TokenUriFormat = "content://{0}.util.provider/userinfo";
    private static readonly string[] SmartBotPackages =
    {
        "com.robocare.smartbot",
        "com.robocare.smartbot.qa",
        "com.robocare.smartbot.dev"
    };
    private string _matchedCamiPackage;
    private const int LoginDetectMaxAttemptCount = 3;
    private const float LoginDetectRetryIntervalSeconds = 0.15f;
    private const float LoginDetectBomiAttemptTimeoutSeconds = 2.5f;

    bool lastFailHaveApp = false;

    private void Start()
    {
        LogApi.Log("LoginTokenReader.Start() called");

        InitDefaultsIfNeeded();

        if (goStoreBtn != null) goStoreBtn.onClick.AddListener(ClickLoginFailBtn);
        if (guestBtn != null) guestBtn.onClick.AddListener(ClickGuestBtn);
        if (camiBtn != null) camiBtn.onClick.AddListener(ClickCamiBtn);
        if (secretBtn2 != null) secretBtn2.onClick.AddListener(ClickSecretBtn);

        if (loginSuccessPanel != null)
        {
            loginSuccessPanel.Shown += () => Block("success");
            loginSuccessPanel.Hidden += () => Unblock("success");
        }

        if (platformSelector != null)
        {
            platformSelector.Opened += () => Block("selector");
            platformSelector.Closed += () => Unblock("selector");
        }

        if (!PlayerPrefs.HasKey(AppLaunchedKey))
        {
            GetDataAsync().Forget();
        }
    }

    private void InitDefaultsIfNeeded()
    {
        if (!PlayerPrefs.HasKey(PrefKeys.GetUserData))
            PlayerPrefs.SetString(PrefKeys.GetUserData, "on");   // or android

        // user 정보 PlayerPrefs
        if (!PlayerPrefs.HasKey(PrefKeys.User))
            PlayerPrefs.SetString(PrefKeys.User, "unknown");
        if (!PlayerPrefs.HasKey(PrefKeys.UserName))
            PlayerPrefs.SetString(PrefKeys.UserName, "unknown");
        if (!PlayerPrefs.HasKey(PrefKeys.PrjId))
            PlayerPrefs.SetString(PrefKeys.PrjId, "테스트");
        if (!PlayerPrefs.HasKey(PrefKeys.PrjType))
            PlayerPrefs.SetString(PrefKeys.PrjType, "테스트");
        if (!PlayerPrefs.HasKey(PrefKeys.Version))
            PlayerPrefs.SetString(PrefKeys.Version, "3.0");
        if (!PlayerPrefs.HasKey(PrefKeys.RobotId))
            PlayerPrefs.SetString(PrefKeys.RobotId, "unknown");
        if (!PlayerPrefs.HasKey(PrefKeys.RobotType))
            PlayerPrefs.SetInt(PrefKeys.RobotType, 0);

        // api 관련 세팅
        if (!PlayerPrefs.HasKey(PrefKeys.GameApi))
            PlayerPrefs.SetString(PrefKeys.GameApi, DefaultEndpoints.GameApi);
        if (!PlayerPrefs.HasKey(PrefKeys.TtsApi))
            PlayerPrefs.SetString(PrefKeys.TtsApi, DefaultEndpoints.TtsApi);

        PlayerPrefs.SetString(PrefKeys.RobotApi, DefaultEndpoints.RobotApi);
        PlayerPrefs.SetString(PrefKeys.Socket, DefaultEndpoints.Socket);
        PlayerPrefs.SetString(PrefKeys.Mjpeg, DefaultEndpoints.Mjpeg);

        if (!PlayerPrefs.HasKey(PrefKeys.RobotInteractionApi))
            PlayerPrefs.SetString(PrefKeys.RobotInteractionApi, DefaultEndpoints.RobotInteractionApi);

        if (!PlayerPrefs.HasKey(PrefKeys.ConnectTimeout))
            PlayerPrefs.SetInt(PrefKeys.ConnectTimeout, 5);
        if (!PlayerPrefs.HasKey(PrefKeys.ReadTimeout))
            PlayerPrefs.SetInt(PrefKeys.ReadTimeout, 10);
        if (!PlayerPrefs.HasKey(PrefKeys.WriteTimeout))
            PlayerPrefs.SetInt(PrefKeys.WriteTimeout, 10);

        PlayerPrefs.Save();
    }

    private void Block(string key)
    {
        if (_locks.Add(key))
        {
            if (blocker != null && !blocker.activeSelf)
                blocker.SetActive(true);
        }
        else
        {
            // 이미 같은 키가 있어도, 혹시 꺼져 있으면 켜준다(안전)
            if (blocker != null && !blocker.activeSelf)
                blocker.SetActive(true);
        }
    }

    private void Unblock(string key)
    {
        if (_locks.Remove(key))
        {
            if (_locks.Count == 0 && blocker != null && blocker.activeSelf)
                blocker.SetActive(false);
        }
    }

    private void CloseFailPanel()
    {
        if (loginFailPanel != null) loginFailPanel.SetActive(false);
        Unblock("fail");
    }

    private bool _busy;
    private async UniTaskVoid GetDataAsync()
    {
        if (_busy) return;
        _busy = true;
        _matchedCamiPackage = null;

        loadingPanel.gameObject.SetActive(true);
        loadingPanel.StartLoading();

        PlatformType? detected = null;
        string failMsg = null;
        bool failHaveApp = false;

        // 성공하면 즉시 다음 단계로 진행한다.
        await loadingPanel.ShowWhile(DetectWithRetryAsync(), 0f);
        lastFailHaveApp = failHaveApp;

        if (detected.HasValue)
            SetPrefsAndOpenSuccess(detected.Value);
        else
            OpenFailPanel(failMsg ?? "로그인 정보를 확인할 수 없습니다.", failHaveApp);

        _busy = false;

        async UniTask DetectWithRetryAsync()
        {
            for (int attempt = 0; attempt < LoginDetectMaxAttemptCount; attempt++)
            {
                var result = await DetectOnceAsync();
                if (result.detectedPlatform.HasValue)
                {
                    detected = result.detectedPlatform;
                    return;
                }

                if (!string.IsNullOrEmpty(result.failMsg))
                    failMsg = result.failMsg;

                failHaveApp = failHaveApp || result.failHaveApp;

                if (attempt >= LoginDetectMaxAttemptCount - 1)
                    break;

                if (LoginDetectRetryIntervalSeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(LoginDetectRetryIntervalSeconds), DelayType.UnscaledDeltaTime);
            }
        }

        async UniTask<(PlatformType? detectedPlatform, string failMsg, bool failHaveApp)> DetectOnceAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string camiFailMsg = null;

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject contentResolver = context.Call<AndroidJavaObject>("getContentResolver"))
                using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    // 패키지별 provider를 순차 확인: 운영 -> QA -> DEV
                    for (int packageIndex = 0; packageIndex < SmartBotPackages.Length; packageIndex++)
                    {
                        string packageName = SmartBotPackages[packageIndex];
                        string tokenUri = string.Format(TokenUriFormat, packageName);

                        try
                        {
                            using (AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", tokenUri))
                            {
                                AndroidJavaObject cursor = contentResolver.Call<AndroidJavaObject>("query", uri, null, null, null, null);

                                try
                                {
                                    if (cursor != null && cursor.Call<bool>("moveToFirst"))
                                    {
                                        _matchedCamiPackage = packageName;

                                        string token = cursor.Call<string>("getString", 0);
                                        if (tokenText != null)
                                            tokenText.text = token;

                                        string[] parts = token.Split('_');
                                        if (parts.Length >= 3)
                                        {
                                            string userId = parts[0];
                                            string userName = parts[1];
                                            string robotId = parts[2];

                                            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(robotId))
                                            {
                                                _userId = userId;
                                                _userName = userName;
                                                _robotId = robotId;
                                                if (parts.Length >= 4) _prjId = parts[3];
                                                if (parts.Length >= 5) _prjName = parts[4];

                                                LogApi.Log($"userId: {_userId}, userName: {_userName}, robotId: {_robotId}, prjId: {_prjId}, prjName: {_prjName}");
                                                return (PlatformType.CAMI, null, false);
                                            }

                                            camiFailMsg = "케미프렌즈 앱에서 받아온 로그인 정보가 비어 있습니다.\n\n케미 프렌즈 앱에서 다시 로그인 진행해주세요!";
                                            continue;
                                        }

                                        camiFailMsg = "케미프렌즈 앱에서 받아온 로그인 정보가 형식과 다릅니다.\n\n케미 프렌즈 앱에서 다시 로그인 진행해주세요!";
                                        continue;
                                    }
                                }
                                finally
                                {
                                    if (cursor != null)
                                        cursor.Call("close");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogApi.LogWarning($"카미 토큰 조회 실패 ({packageName}) : {ex.Message}");
                        }
                    }

                    if (await TryDetectBomiAsync(LoginDetectBomiAttemptTimeoutSeconds))
                    {
                        _robotId = PlayerPrefs.GetString(PrefKeys.RobotId);
                        _userId = PlayerPrefs.GetString(PrefKeys.User);
                        _userName = PlayerPrefs.GetString(PrefKeys.UserName);
                        return (PlatformType.BOMI1, null, false);
                    }

                    if (!string.IsNullOrEmpty(camiFailMsg))
                        return (null, camiFailMsg, true);

                    return (null, "케미 프렌즈 앱이 없습니다.<br>케미 프렌즈 앱을 설치해주세요.<br>게스트 모드 선택 시 점수를 저장하지 않고 게임 진행이 가능합니다.", false);
                }
            }
            catch (Exception ex)
            {
                LogApi.LogWarning($"케미 토큰 조회 중 예외 : {ex.Message}");
            }

            if (await TryDetectBomiAsync(LoginDetectBomiAttemptTimeoutSeconds))
            {
                _robotId = PlayerPrefs.GetString(PrefKeys.RobotId);
                _userId = PlayerPrefs.GetString(PrefKeys.User);
                _userName = PlayerPrefs.GetString(PrefKeys.UserName);
                return (PlatformType.BOMI1, null, false);
            }

            return (null, "케미 프렌즈 앱이 없습니다.<br>케미 프렌즈 앱을 설치해주세요.<br>게스트 모드 선택 시 점수를 저장하지 않고 게임 진행이 가능합니다.", false);
#else
            LogApi.Log("Editor: Android-specific code skipped.");
            await UniTask.Yield();
            return (PlatformType.BOMI1, null, false);
#endif
        }
    }

    private async UniTask<bool> TryDetectBomiAsync(float timeoutSeconds)
    {
        // try
        // {
        //     var (isTimeout, ok) = await HibomiApiClient.Instance()
        //         .GetRobotID()
        //         .TimeoutWithoutException(TimeSpan.FromSeconds(timeoutSeconds), DelayType.UnscaledDeltaTime);

        //     if (isTimeout)
        //         return false;

        //     if (ok) return true;
        //     else return false;
        // }
        // catch (System.Exception e)
        // {
        //     LogApi.LogWarning($"Bomi 감지 실패: {e.Message}");
        //     return false;
        // }
        return false; // HibomiApiClient 설정 후 옆에 코드 지우고 위에 주석 풀기 
    }

    private void ClickCamiBtn()
    {
        CloseFailPanel();
        StartCoroutine(GoApp());
    }

    private void ClickGuestBtn()
    {
        // 케미 앱이 설치되어 있고 failHaveApp=true 라면 -> Cami-Guest
        if (lastFailHaveApp)
        {
            // CAMI Guest
            _userId = "guest";
            _userName = "guest";
            _robotId = "guest";

            PlayerPrefs.SetString(PrefKeys.User, "guest");
            PlayerPrefs.SetString(PrefKeys.UserName, "guest");
            PlayerPrefs.SetString(PrefKeys.RobotId, "guest");

            // CAMI-Guest는 gamemode = R
            ApplyPlatformSettings(PlatformType.CAMI);

            // score 저장 off
            PlayerPrefs.SetString(PrefKeys.ScoreMode, "android");
            SetGuestMode(true);

            PlayerPrefs.Save();

            CloseFailPanel();
            SetPrefsAndOpenSuccess(PlatformType.CAMI);
            return;
        }

        // ------------------------------
        // 케미 앱 없음 -> Others Guest
        // ------------------------------
        _userId = "guest";
        _userName = "guest";
        _robotId = "guest";

        PlayerPrefs.SetString(PrefKeys.User, "guest");
        PlayerPrefs.SetString(PrefKeys.UserName, "guest");
        PlayerPrefs.SetString(PrefKeys.RobotId, "guest");

        // Others는 gamemode = A
        ApplyPlatformSettings(PlatformType.Others);

        PlayerPrefs.SetString(PrefKeys.ScoreMode, "android");
        SetGuestMode(true);

        PlayerPrefs.Save();

        CloseFailPanel();
        SetPrefsAndOpenSuccess(PlatformType.Others);
    }

    private void ClickLoginFailBtn()
    {
        CloseFailPanel();
        StartCoroutine(GoApp()); // 앱 스토어로 보내기
        //Application.Quit();
    }

    private void ClickSecretBtn()
    {
        PlayerPrefs.SetInt(AppLaunchedKey, 1);
        PlayerPrefs.Save();
    }

    private void OpenFailPanel(string info, bool haveApp)
    {
        loadingPanel.StopLoading();

        if (haveApp)
        {
            goStoreBtn.gameObject.SetActive(false);
            guestBtn.gameObject.SetActive(false);
            camiBtn.gameObject.SetActive(true);
        }
        else
        {
            goStoreBtn.gameObject.SetActive(true);
            guestBtn.gameObject.SetActive(true);
            camiBtn.gameObject.SetActive(false);
        }
        loginFailInfoText.text = info;
        loginFailPanel.SetActive(true);
        Block("fail");
    }

    private void SetPrefsAndOpenSuccess(PlatformType platform)
    {
        loadingPanel.StopLoading();

        // guest 여부 먼저 판정 (Editor 덮어쓰기 전에)
        bool isGuest = (_userId == "guest" || _robotId == "guest");

        // Editor 환경 임의 값 (guest가 아닐 때만)
        // DEV:  devapi.hibomi.com — 개발서버 (반복 테스트 OK)
        //   userId: 282679f4-0646-4cf4-8441-2c12074d1868 / 정병훈 / B10000-001
        // PROD: api.hibomi.com — 상용서버 (테스트 최소화)
        //   userId: fdb1c19b-5de5-4e3f-89f6-c85f02cd94a0 / 에이징 / CF0000-00010
#if UNITY_EDITOR
        if (!isGuest)
        {
            _userId = "282679f4-0646-4cf4-8441-2c12074d1868";
            _userName = "정병훈";
            _robotId = "B10000-001";
            _prjId = "";
            _prjName = "TEST-CONTENT";
        }
#endif

        PlayerPrefs.SetString(PrefKeys.User, _userId);
        PlayerPrefs.SetString(PrefKeys.UserName, _userName);
        PlayerPrefs.SetString(PrefKeys.RobotId, _robotId);
        PlayerPrefs.SetString(PrefKeys.PrjId, _prjId ?? "");
        PlayerPrefs.SetString(PrefKeys.PrjName, _prjName ?? "");
        PlayerPrefs.Save();

        ApplyPlatformSettings(platform);

        PlayerPrefs.SetInt(AppLaunchedKey, 1);
        PlayerPrefs.Save();

        SetGuestMode(isGuest);

        // HibomiApiClient 설정 후 아래 주석 풀기 
        // if (!isGuest)
        //     _ = HibomiApiClient.Instance().GetTokenAndSetCookie();

        loginSuccessPanel.OnConfirmPlatform = () =>
        {
            LogApi.Log($"플랫폼 확정, type:{robotTypeInt}, robotid:{_robotId}, username:{_userName}");
            loginSuccessPanel.ForceClose();

            if (currentPlatform == PlatformType.CAMI && tosPanel != null)
            {
                UnityEngine.Events.UnityAction onTosClosedAction = null;
                onTosClosedAction = () =>
                {
                    // 한 번 실행 후 리스너 제거 (중복 실행 방지)
                    tosPanel.onTermsClosed.RemoveListener(onTosClosedAction);

                    // 게임 진입
                    OnLoginCompleted();
                };

                // 리스너 등록 후 체크 실행
                tosPanel.onTermsClosed.AddListener(onTosClosedAction);
                tosPanel.CheckAndOpenIfNeeded("cami confirmed");
            }
            else
            {
                // 케미가 아니면 바로 다음 단계로 진행
                OnLoginCompleted();
            }
        };

        loginSuccessPanel.OnChangePlatform = () =>
        {
            // "아니오" -> 플랫폼 직접 선택 UI 열기
            loginSuccessPanel.ForceClose();

            platformSelector.gameObject.SetActive(true);
            platformSelector.Open(currentPlatform, OnPlatformSelected);
        };

        loginSuccessPanel.gameObject.SetActive(true);
        loginSuccessPanel.Show(_userName, _robotId, currentPlatform);
    }

    private void ApplyPlatformSettings(PlatformType platform)
    {
        currentPlatform = platform;
        robotTypeInt = (int)platform;

        GlobalStatics.platformType = currentPlatform;
        GlobalStatics.robotTypeInt = robotTypeInt;

        // gamemode
        string gamemode = "A";
        switch (platform)
        {
            case PlatformType.BOMI2:
            case PlatformType.CAMI:
                gamemode = "R";
                break;
            case PlatformType.BOMI1:
            case PlatformType.Others:
            default:
                gamemode = "A";
                break;
        }

        // svpMode
        string svpMode;
        string getuserdata;
        switch (platform)
        {
            case PlatformType.BOMI1:
                svpMode = "on";
                getuserdata = "on";
                break;
            case PlatformType.BOMI2:
            case PlatformType.CAMI:
            case PlatformType.Others:
            default:
                svpMode = "off";
                getuserdata = "off";
                break;
        }

        if (!PlayerPrefs.HasKey(PrefKeys.SvpMode))
            PlayerPrefs.SetString(PrefKeys.SvpMode, svpMode);      // or off
        if (!PlayerPrefs.HasKey(PrefKeys.GetUserData))
            PlayerPrefs.SetString(PrefKeys.GetUserData, getuserdata);

        // scoremode
        string scoreMode;
        switch (platform)
        {
            case PlatformType.Others:
                scoreMode = "android";
                break;
            case PlatformType.BOMI1:
            case PlatformType.BOMI2:
            case PlatformType.CAMI:
            default:
                scoreMode = "api";
                break;
        }

        // Lite가 어떻게 될 지는 모르겠으나,
        if (!PlayerPrefs.HasKey(PrefKeys.ScoreMode))
            PlayerPrefs.SetString(PrefKeys.ScoreMode, scoreMode);

        PlayerPrefs.SetString(PrefKeys.GameMode, gamemode);
        PlayerPrefs.SetInt(PrefKeys.RobotType, robotTypeInt);
        PlayerPrefs.Save();

        // 이벤트 날리기
        GlobalStatics.RaiseChangePlatform(currentPlatform);
    }

    private void OnPlatformSelected(PlatformType selected)
    {
        // Others(Lite) 선택 시 guest 처리
        if (selected == PlatformType.Others)
        {
            _userId = "guest";
            _userName = "guest";
            _robotId = "guest";

            PlayerPrefs.SetString(PrefKeys.User, "guest");
            PlayerPrefs.SetString(PrefKeys.UserName, "guest");
            PlayerPrefs.SetString(PrefKeys.RobotId, "guest");
        }

        SetPrefsAndOpenSuccess(selected);
    }

    private void OnLoginCompleted()
    {
        // bomi_coggames 로그인 완료 후 후속 진입 포인트
    }

    private IEnumerator GoApp()
    {
        yield return new WaitForSeconds(1);
        LaunchExternalAppIfInstalled();
    }

    public void LaunchExternalAppIfInstalled()
    {
        string targetPackage = string.IsNullOrEmpty(_matchedCamiPackage) ? SmartBotPackages[0] : _matchedCamiPackage;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
            {
                AndroidJavaObject launchIntent = packageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", targetPackage);

                if (launchIntent != null)
                {
                    currentActivity.Call("startActivity", launchIntent);
                    launchIntent.Dispose();
                    LogApi.Log($"외부 앱 실행 성공: {targetPackage}");
                }
                else
                {
                    LogApi.LogWarning($"외부 앱({targetPackage})이 설치되어 있지 않습니다.");
                    // 필요시 구글 플레이 이동 가능:
                    Application.OpenURL($"market://details?id={targetPackage}");
                }
            }
        }
        catch (System.Exception e)
        {
            LogApi.LogError("앱 실행 중 예외 발생: " + e.Message);
        }
#else
        LogApi.Log($"에디터에서는 실행되지 않음 ({targetPackage})");
#endif
    }

    private void OnApplicationQuit()
    {
        // 앱 종료 시 초기화 키 삭제
        // => 다음 앱 실행 시 GetData 하기 위함
        if (PlayerPrefs.HasKey(AppLaunchedKey))
        {
            PlayerPrefs.DeleteKey(AppLaunchedKey);
            PlayerPrefs.Save();
            Application.Quit();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Application.Quit();
    }

    public void SetGuestMode(bool isGuest)
    {
        if (isGuest)
        {
            LogApi.LogWarning($"{GetType()}::Guest Mode 입니다.");
            PlayerPrefs.SetString(PrefKeys.ScoreMode, "android");
            PlayerPrefs.Save();
        }
        else
        {
            LogApi.Log($"{GetType()}::Guest Mode 아닙니다.");
            // 비-게스트: 기존 PlayerPrefs 값 유지 (사용자 설정 존중)
        }
    }
}

public static class GlobalStatics
{
    public static string selectedCategory;
    public static string selectedGame;

    public static PlatformType platformType;
    public static int robotTypeInt;

    public static Action<PlatformType> OnChangedPlatform;
    public static void RaiseChangePlatform(PlatformType type) => OnChangedPlatform?.Invoke(type);
}

}
