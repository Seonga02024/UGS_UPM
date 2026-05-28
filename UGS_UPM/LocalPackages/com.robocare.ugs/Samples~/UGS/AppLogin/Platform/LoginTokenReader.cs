using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RoboCare.UGS;

public class LoginTokenReader : SingletonBehaviour<LoginTokenReader>
{
    [Header("Loading Login")]
    [SerializeField] private LoginLoadingPanel loadingPanel;

    [Header("Login Success")]
    [SerializeField] private LoginSuccessPanel loginSuccessPanel;
    [SerializeField] private TMP_Text loginSuccessInfoText;

    [Header("Login Failed")]
    [SerializeField] private GameObject loginFailPanel;
    [SerializeField] private TMP_Text failTitleText;       // 상황별 타이틀. Inspector 에서 Header/Title TMP 연결
    [SerializeField] private TMP_Text loginFailInfoText;   // 본문 메시지
    [SerializeField] private TMP_Text failErrorCodeText;   // 하단 작은 빨간 글씨. 정규 errorCode 제외 시 표시
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

    [Header("Login Bridge (프로젝트별 구현체 연결 — 선택)")]
    [Tooltip("로그인 완료 시점에 프로젝트별 동작(로봇 이벤트 발화 등)을 실행하려면\nLoginBridgeBase 를 상속한 구현체를 씬에 배치 후 연결.\n연결 안 하면 null-safe 로 무시됨.")]
    [SerializeField] private LoginBridgeBase bridge;

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
    private string _gameAccessToken = "";
    private string _fcmToken = "";

    private int robotTypeInt = 0;
    public PlatformType currentPlatform;

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

#if UNITY_IOS
    // ─── iOS 메인앱 연동 상수 (Phase 2 대비, 현재 빈값 — Guest 모드로 진입) ───
    // Docs/IOSPlatform/02-login-system.md 참조.
    // 메인앱 iOS 출시 시 아래 값 설정:
    //   IOSMainAppScheme = "camifriends://" (예시)
    //   IOSAppStoreUrl   = "https://apps.apple.com/app/id1234567890" (예시)
    private const string IOSMainAppScheme = "";
    private const string IOSAppStoreUrl = "";
    private const string IOSNoMainAppMessage =
        "현재 iOS 버전에서는 게스트 모드로 이용 가능합니다.\n점수 저장 없이 게임을 즐길 수 있습니다.";
#endif

    bool lastFailHaveApp = false;
    public static bool IsFirstAppOpen = true;

    // 메인앱이 공식 제공하는 정규 errorCode (팝업에 code 숨김, message만 노출)
    private static readonly HashSet<string> KnownProviderErrorCodes = new HashSet<string>
    {
        "SESSION_NOT_READY",
        "TOKEN_NOT_READY",
        "PROVIDER_INIT_ERROR",
    };

    private void Start()
    {
        Debug.Log("LoginTokenReader.Start() called");

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

        if (!PlayerPrefs.HasKey(AppLaunchedKey) || IsFirstAppOpen)
        {
            IsFirstAppOpen = false;
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
            PlayerPrefs.SetString(PrefKeys.RobotInteractionApi, DefaultEndpoints.RobotInteractionApiProd);

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

        PlatformType? detected = null;
        string failMsg = null;
        string failErrorCode = null;
        bool failHaveApp = false;

        try
        {
            loadingPanel.gameObject.SetActive(true);
            loadingPanel.StartLoading();

            // 성공하면 즉시 다음 단계로 진행한다.
            await loadingPanel.ShowWhile(DetectWithRetryAsync(), 0f);
            lastFailHaveApp = failHaveApp;

            if (detected.HasValue)
                SetPrefsAndOpenSuccess(detected.Value);
            else
                OpenFailPanel(failMsg ?? "로그인 정보를 확인할 수 없습니다.", failHaveApp, failErrorCode);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Login] GetDataAsync exception: {ex.Message}\n{ex.StackTrace}");
            OpenFailPanel("로그인 중 오류가 발생했습니다.", false, "GET_DATA_EXCEPTION");
        }
        finally
        {
            _busy = false;
        }

        async UniTask DetectWithRetryAsync()
        {
            Debug.Log($"[Login] === Login detect start (max {LoginDetectMaxAttemptCount} attempts) ===");
            for (int attempt = 0; attempt < LoginDetectMaxAttemptCount; attempt++)
            {
                Debug.Log($"[Login] >> Attempt {attempt + 1}/{LoginDetectMaxAttemptCount}");
                var result = await DetectOnceAsync();
                if (result.detectedPlatform.HasValue)
                {
                    detected = result.detectedPlatform;
                    Debug.Log($"[Login] OK Detected! platform={detected.Value} (attempt {attempt + 1})");
                    return;
                }

                Debug.Log($"[Login] X Attempt {attempt + 1} failed — code:[{result.errorCode}], msg:[{result.failMsg}], haveApp:{result.failHaveApp}");

                if (!string.IsNullOrEmpty(result.failMsg)) failMsg = result.failMsg;
                if (!string.IsNullOrEmpty(result.errorCode)) failErrorCode = result.errorCode;
                failHaveApp = failHaveApp || result.failHaveApp;

                if (attempt >= LoginDetectMaxAttemptCount - 1)
                {
                    Debug.LogWarning($"[Login] FAIL Max attempts reached — finalCode:[{failErrorCode}], finalMsg:[{failMsg}], failHaveApp:{failHaveApp}");
                    break;
                }

                if (LoginDetectRetryIntervalSeconds > 0f)
                {
                    Debug.Log($"[Login] Waiting {LoginDetectRetryIntervalSeconds}s before retry...");
                    await UniTask.Delay(TimeSpan.FromSeconds(LoginDetectRetryIntervalSeconds), DelayType.UnscaledDeltaTime);
                }
            }
        }

        async UniTask<(PlatformType? detectedPlatform, string failMsg, bool failHaveApp, string errorCode)> DetectOnceAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string lastFailMsg = null;
            string lastErrorCode = null;

            // 1단계: 설치된 CAMI 패키지 확정 (PackageManager 기반)
            var installedPackages = FindInstalledCamiPackages();
            bool hasAnyInstalled = installedPackages.Count > 0;
            Debug.Log($"[Login] Installed CAMI packages: {installedPackages.Count}/{SmartBotPackages.Length} — [{string.Join(", ", installedPackages)}]");

            // 2단계: 설치된 패키지만 Provider query
            if (hasAnyInstalled)
            {
                try
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaObject contentResolver = context.Call<AndroidJavaObject>("getContentResolver"))
                    using (AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri"))
                    {
                        foreach (var packageName in installedPackages)
                        {
                            string packageLabel = PackageLabel(packageName);
                            string tokenUri = string.Format(TokenUriFormat, packageName);
                            Debug.Log($"[Login] Query provider — {packageLabel}({packageName}) -> URI:{tokenUri}");

                            string rawToken = null;
                            string queryErrorCode = null;
                            string queryErrorMsg = null;

                            try
                            {
                                using (AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", tokenUri))
                                {
                                    AndroidJavaObject cursor = contentResolver.Call<AndroidJavaObject>("query", uri, null, null, null, null);
                                    try
                                    {
                                        bool cursorValid = cursor != null;
                                        if (!cursorValid)
                                        {
                                            queryErrorCode = "PROVIDER_NULL_CURSOR";
                                            queryErrorMsg = null; // body 문구는 OpenFailPanel 에서 errorCode 로 매핑
                                            Debug.LogWarning($"[Login] {packageLabel} NULL CURSOR");
                                        }
                                        else
                                        {
                                            int rowCount = cursor.Call<int>("getCount");
                                            int colCount = cursor.Call<int>("getColumnCount");
                                            bool movedToFirst = cursor.Call<bool>("moveToFirst");
                                            Debug.Log($"[Login] {packageLabel} — rowCount:{rowCount}, colCount:{colCount}, movedToFirst:{movedToFirst}");

                                            if (!movedToFirst)
                                            {
                                                queryErrorCode = "SESSION_NOT_READY";
                                                queryErrorMsg = null;
                                                _matchedCamiPackage = packageName; // 앱 이동용
                                                Debug.LogWarning($"[Login] {packageLabel} EMPTY CURSOR — 로그인 세션 없음");
                                            }
                                            else
                                            {
                                                // 디버그용 컬럼 덤프 (Logcat 추적용)
                                                for (int ci = 0; ci < colCount; ci++)
                                                {
                                                    string colName = cursor.Call<string>("getColumnName", ci);
                                                    bool isNull = cursor.Call<bool>("isNull", ci);
                                                    string colVal = isNull ? "<NULL>" : cursor.Call<string>("getString", ci);
                                                    Debug.Log($"<color=green>[Login]   column[{ci}] name={colName}, isNull={isNull}, value=[{colVal}]</color>");
                                                }

                                                rawToken = cursor.Call<string>("getString", 0);
                                                _matchedCamiPackage = packageName;
                                                Debug.Log($"<color=green>[Login] Provider OK from {packageLabel}. rawToken length={rawToken?.Length ?? -1}</color>");
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (cursor != null) cursor.Call("close");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                queryErrorCode = "PROVIDER_EXCEPTION";
                                queryErrorMsg = ex.Message;
                                Debug.LogWarning($"[Login] {packageLabel} EXCEPTION: {ex.Message}");
                            }

                            // 쿼리 결과 처리
                            if (!string.IsNullOrEmpty(rawToken))
                            {
                                if (tokenText != null) tokenText.text = rawToken;

                                // JSON 우선 + 레거시 underscore fallback (LoginTokenParser)
                                var outcome = LoginTokenParser.Parse(rawToken);
                                Debug.Log($"[Login] Parse status:{outcome.Status}, code:[{outcome.ErrorCode}]");

                                if (outcome.Status == LoginParseStatus.Success)
                                {
                                    var d = outcome.Data;
                                    _userId = d.userId;
                                    _userName = d.userName;
                                    _robotId = d.robotId ?? ""; // 로봇 미등록 시 빈 문자열 (로그인은 성공 취급)
                                    _prjId = d.prjId ?? "";
                                    _prjName = d.prjName ?? "";
                                    _gameAccessToken = d.gameAccessToken ?? "";
                                    _fcmToken = d.fcmToken ?? "";

                                    // 운영 vs QA/DEV 분기로 Robot Interaction URL 선택
                                    string robotInteractionUrl = packageName == SmartBotPackages[0]
                                        ? DefaultEndpoints.RobotInteractionApiProd
                                        : DefaultEndpoints.RobotInteractionApiDev;
                                    PlayerPrefs.SetString(PrefKeys.RobotInteractionApi, robotInteractionUrl);
                                    PlayerPrefs.Save();

                                    Debug.Log($"<color=green>[Login] OK CAMI login success! mainApp={packageLabel}({packageName})</color>");
                                    Debug.Log($"<color=green>[Login] userId:{_userId}, userName:{_userName}, robotId:[{_robotId}] (empty={string.IsNullOrEmpty(_robotId)})</color>");
                                    Debug.Log($"<color=green>[Login] prjId:[{_prjId}], prjName:[{_prjName}], tokenLen:{_gameAccessToken.Length}, fcmLen:{_fcmToken.Length}</color>");
                                    Debug.Log($"<color=green>[Login] RobotInteractionApi -> {robotInteractionUrl}</color>");
                                    return (PlatformType.CAMI, null, true, null);
                                }

                                // JSON error (status=error) 또는 파싱 실패
                                lastFailMsg = outcome.Message;
                                lastErrorCode = outcome.ErrorCode
                                    ?? (outcome.Status == LoginParseStatus.FormatMismatch ? "LOCAL_PARSE_FAIL"
                                        : outcome.Status == LoginParseStatus.EmptyToken ? "SESSION_NOT_READY"
                                        : "UNKNOWN_PARSE");
                                continue;
                            }

                            // 쿼리 실패 케이스 (cursor null / exception / session empty)
                            if (!string.IsNullOrEmpty(queryErrorCode))
                            {
                                lastFailMsg = queryErrorMsg;
                                lastErrorCode = queryErrorCode;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Login] Global CAMI access exception: {ex.Message}\n{ex.StackTrace}");
                    if (string.IsNullOrEmpty(lastErrorCode))
                    {
                        lastErrorCode = "PROVIDER_GLOBAL_EXCEPTION";
                        lastFailMsg = ex.Message;
                    }
                }
            }

            // 3단계: BOMI fallback (설치 여부와 무관, 기존 동작 유지)
            Debug.Log("[Login] Trying BOMI fallback...");
            if (await TryDetectBomiAsync(LoginDetectBomiAttemptTimeoutSeconds))
            {
                _robotId = PlayerPrefs.GetString(PrefKeys.RobotId);
                _userId = PlayerPrefs.GetString(PrefKeys.User);
                _userName = PlayerPrefs.GetString(PrefKeys.UserName);
                Debug.Log($"[Login] OK BOMI detected! userId:{_userId}, userName:{_userName}, robotId:{_robotId}");
                return (PlatformType.BOMI1, null, false, null);
            }

            // 4단계: 최종 실패 분류
            if (hasAnyInstalled)
            {
                // 앱은 설치되어 있음 → CamiBtn 활성 + errorCode 전달
                Debug.LogWarning($"[Login] CAMI installed but detection failed — lastCode:[{lastErrorCode}], msg:[{lastFailMsg}]");
                return (null, lastFailMsg, true, lastErrorCode ?? "UNKNOWN");
            }

            // 진짜 설치 안됨 → StoreBtn/GuestBtn 활성
            Debug.LogWarning("[Login] No CAMI app installed and BOMI fallback failed");
            return (null, null, false, "APP_NOT_INSTALLED");

#elif UNITY_IOS && !UNITY_EDITOR
            // iOS: 메인앱 Keychain 연동 미구현 → Guest 모드 유도 (Phase 2 대비).
            //      Docs/IOSPlatform/02-login-system.md 지침.
            //      (BOMI1 fallback 은 Android 로봇 서버 연결이라 iOS 부적절)
            //      haveApp=false → OpenFailPanel 에서 iOS 분기로 Guest 버튼만 노출.
            Debug.Log("[Login] iOS: 메인앱 미존재 → Guest 유도 (APP_NOT_INSTALLED)");
            await UniTask.Yield();
            return (null, IOSNoMainAppMessage, false, "APP_NOT_INSTALLED");
#else
            Debug.Log("[Login] Editor: 네이티브 감지 스킵 — BOMI1 로 fallback.");
            await UniTask.Yield();
            return (PlatformType.BOMI1, null, false, null);
#endif
        }
    }

    private async UniTask<bool> TryDetectBomiAsync(float timeoutSeconds)
    {
        try
        {
            var (isTimeout, ok) = await HibomiApiClient.Instance()
                .GetRobotID()
                .TimeoutWithoutException(TimeSpan.FromSeconds(timeoutSeconds), DelayType.UnscaledDeltaTime);

            if (isTimeout || !ok)
                return false;

            await UniTask.DelayFrame(2);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Bomi 감지 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// CAMI 패키지 중 실제 설치된 것만 반환 (PackageManager 기반).
    /// AndroidManifest &lt;queries&gt; 선언 필요 (이미 포함됨).
    /// </summary>
    private List<string> FindInstalledCamiPackages()
    {
        var installed = new List<string>();
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject pm = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
            {
                foreach (var pkg in SmartBotPackages)
                {
                    try
                    {
                        using (AndroidJavaObject info = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 0))
                        {
                            if (info != null)
                            {
                                installed.Add(pkg);
                                Debug.Log($"[Login] Package installed: {pkg}");
                            }
                        }
                    }
                    catch (AndroidJavaException)
                    {
                        // NameNotFoundException → 미설치
                        Debug.Log($"[Login] Package NOT installed: {pkg}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Login] FindInstalledCamiPackages exception: {ex.Message}");
        }
#endif
        return installed;
    }

    private string PackageLabel(string pkg)
    {
        if (SmartBotPackages.Length > 0 && pkg == SmartBotPackages[0]) return "PROD";
        if (SmartBotPackages.Length > 1 && pkg == SmartBotPackages[1]) return "QA";
        if (SmartBotPackages.Length > 2 && pkg == SmartBotPackages[2]) return "DEV";
        return "UNKNOWN";
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

    private void OpenFailPanel(string info, bool haveApp, string errorCode = null)
    {
        loadingPanel.StopLoading();

#if UNITY_IOS && !UNITY_EDITOR
        // iOS: 메인앱 없음 → Guest 버튼만 노출 (App Store/Cami 버튼 숨김).
        //      Apple HIG — 외부 스토어 유도/외부 앱 권유 문구 최소화.
        goStoreBtn.gameObject.SetActive(false);
        camiBtn.gameObject.SetActive(false);
        guestBtn.gameObject.SetActive(true);
#else
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
#endif

        // 상황별 title/body 매핑. errorCode 있으면 매핑된 친근한 문구로, 없으면 info fallback.
        var pres = GetFailPresentation(errorCode);
        if (failTitleText != null)
            failTitleText.text = string.IsNullOrEmpty(pres.title) ? "로그인" : pres.title;

        // 본문: 자체 친근 문구 (시니어용). 메인앱 message 는 본문에 넣지 않음.
        loginFailInfoText.text = !string.IsNullOrEmpty(pres.body)
            ? pres.body
            : "로그인 정보를 확인할 수 없습니다.";

        // 하단: errorCode + 원본 message (개발자/운영자가 캡처로 원인 파악용, 연한 회색 권장)
        if (failErrorCodeText != null)
        {
            if (!string.IsNullOrEmpty(errorCode))
            {
                string codeDisplay = !string.IsNullOrEmpty(info)
                    ? $"[{errorCode}] {info}"
                    : $"[{errorCode}]";
                failErrorCodeText.text = codeDisplay;
                failErrorCodeText.gameObject.SetActive(true);
                Debug.Log($"[Login] FailPanel 하단 표시: {codeDisplay}");
            }
            else
            {
                failErrorCodeText.text = "";
                failErrorCodeText.gameObject.SetActive(false);
            }
        }

        loginFailPanel.SetActive(true);
        Block("fail");
    }

    private struct FailPresentation
    {
        public string title;
        public string body;
    }

    /// <summary>
    /// errorCode 별로 사용자에게 보여줄 Title/Body 문구 매핑.
    /// - 정규 errorCode(SESSION_NOT_READY/TOKEN_NOT_READY/PROVIDER_INIT_ERROR): 메인앱 message 가 있으면 body 는 그걸 우선 사용.
    ///   이 매핑은 title + fallback body 역할만 함.
    /// - 문구 톤은 기존 하드코딩 문구 ("케미프렌즈 앱"/"케미 프렌즈 앱", "~해주세요!" 종결) 를 유지.
    /// - 본문은 "해결방법" 중심으로 작성 (사용자가 다음에 뭘 해야 하는지).
    /// </summary>
    private static FailPresentation GetFailPresentation(string errorCode)
    {
        switch (errorCode)
        {
            // ── 앱 미설치 ──
            case "APP_NOT_INSTALLED":
                return new FailPresentation
                {
                    title = "케미 앱 설치가 필요해요",
                    body = "케미 프렌즈 앱이 설치되어 있지 않습니다.\n앱 설치 후 이용하시거나, 게스트 모드로 가볍게 즐겨보세요!\n(게스트 모드는 점수가 저장되지 않습니다.)"
                };

            // ── 로그인 세션 없음 ──
            case "SESSION_NOT_READY":
                return new FailPresentation
                {
                    title = "로그인이 필요해요",
                    body = "케미 프렌즈 앱에 로그인이 되어 있지 않습니다.\n아래 버튼을 눌러 로그인 후 다시 시도해주세요!"
                };

            // ── PIN 인증 미완료 ──
            case "TOKEN_NOT_READY":
                return new FailPresentation
                {
                    title = "비밀번호(PIN) 인증이 필요해요",
                    body = "케미 프렌즈 앱에서 인증이 완료되지 않았습니다.\n안전한 이용을 위해 앱으로 이동하여 인증을 마쳐주세요."
                };

            // ── 통합: 메인앱 내부 오류 + Provider 계열 + 파싱 실패 (4~7번 통합) ──
            // 시니어 UX: 기술적 분류는 Logcat 에서 errorCode 로 구분, 사용자에게는 통일 문구
            case "PROVIDER_INIT_ERROR":
            case "PROVIDER_EXCEPTION":
            case "PROVIDER_NULL_CURSOR":
            case "PROVIDER_GLOBAL_EXCEPTION":
            case "LOCAL_PARSE_FAIL":
            case "UNKNOWN_PARSE":
            case "GET_DATA_EXCEPTION":
                return new FailPresentation
                {
                    title = "연결에 문제가 생겼어요",
                    body = "케미 프렌즈 앱과 연결하는 중 일시적인 오류가 발생했습니다.\n앱을 다시 실행한 후 시도해주세요!"
                };

            default:
                return new FailPresentation
                {
                    title = "연결에 문제가 생겼어요",
                    body = "케미 프렌즈 앱과 연결하는 중 일시적인 오류가 발생했습니다.\n앱을 다시 실행한 후 시도해주세요!"
                };
        }
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

        if (!isGuest)
            _ = HibomiApiClient.Instance().GetTokenAndSetCookie();

        // RobotApiClient 는 본 프로젝트(리듬게임)에 없음 — Migration Prompt 지침에 따라 비활성화.
        // if (RobotApiClient.Instance != null)
        // {
        //     RobotApiClient.Instance.Refresh(); // RobotId 등 PlayerPrefs 값 재로드
        //     if (!string.IsNullOrEmpty(_gameAccessToken))
        //         RobotApiClient.Instance.SetToken(_gameAccessToken);
        // }

        loginSuccessPanel.OnConfirmPlatform = () =>
        {
            Debug.Log($"플랫폼 확정, type:{robotTypeInt}, robotid:{_robotId}, username:{_userName}");
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
        loginSuccessPanel.Show(_userName, _robotId, currentPlatform, !string.IsNullOrEmpty(_gameAccessToken));
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
                svpMode = "off";
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
        // 프로젝트별 훅 (Bridge 에 위임).
        // Bridge 미연결이면 아무것도 안 함 (null-safe).
        // 프로젝트에서 로그인 완료 시점에 발화해야 할 이벤트(로봇 이벤트 등)가 있으면
        // LoginBridgeBase 를 상속한 구현체에서 OnLoginCompleted 오버라이드 후 Inspector 연결.
        if (bridge != null)
            bridge.OnLoginCompleted(CreateCurrentLoginResult());
    }

    /// <summary>
    /// 현재 LoginTokenReader 의 내부 상태를 LoginResult 로 snapshot.
    /// Bridge 호출 시 전달용.
    /// </summary>
    private LoginResult CreateCurrentLoginResult() => new LoginResult
    {
        userId = _userId,
        userName = _userName,
        robotId = _robotId,
        prjId = _prjId,
        prjName = _prjName,
        gameAccessToken = _gameAccessToken,
        fcmToken = _fcmToken,
        platform = currentPlatform,
        sourcePackage = _matchedCamiPackage,
    };

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
                    Debug.Log($"외부 앱 실행 성공: {targetPackage}");
                }
                else
                {
                    Debug.LogWarning($"외부 앱({targetPackage})이 설치되어 있지 않습니다.");
                    // 필요시 구글 플레이 이동 가능:
                    Application.OpenURL($"market://details?id={targetPackage}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("앱 실행 중 예외 발생: " + e.Message);
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS: URL Scheme 으로 메인앱 실행 시도, 실패 시 App Store 이동.
        //      Phase 2 에서 상수 값 설정 후 활성화 예정.
        if (!string.IsNullOrEmpty(IOSMainAppScheme))
        {
            Application.OpenURL(IOSMainAppScheme);
            Debug.Log($"[Login] iOS 메인앱 URL Scheme 호출: {IOSMainAppScheme}");
        }
        else if (!string.IsNullOrEmpty(IOSAppStoreUrl))
        {
            Application.OpenURL(IOSAppStoreUrl);
            Debug.Log($"[Login] iOS App Store 이동: {IOSAppStoreUrl}");
        }
        else
        {
            Debug.Log("[Login] iOS 메인앱 URL 미설정 (Phase 2 대비 빈값)");
        }
#else
        Debug.Log($"에디터에서는 실행되지 않음 ({targetPackage})");
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
        }
    }

    private void OnApplicationPause(bool paused)
    {
        // 백그라운드 전환 시 강제 Quit 을 제거.
        // 기존 동작은 Kiosk 요구사항에 가까웠으나, 일반 사용자 시나리오(알림/전화/홈 버튼 등)에서
        // 앱이 튕기는 UX 문제가 있어 제거.
        // CAMI 앱 전환 후 복귀 시나리오(ClickCamiBtn)도 이 때문에 막혔던 것을 함께 해소.

#if UNITY_IOS && !UNITY_EDITOR
        // iOS: OnApplicationQuit 호출이 보장되지 않으므로 (06-app-lifecycle.md 참조)
        //      pause 시점에 AppLaunchedKey 삭제를 백업으로 수행 → 다음 실행 시 재로그인 보장.
        if (paused)
        {
            if (PlayerPrefs.HasKey(AppLaunchedKey))
            {
                PlayerPrefs.DeleteKey(AppLaunchedKey);
                PlayerPrefs.Save();
                Debug.Log("[Login] iOS pause: AppLaunchedKey 삭제 (재로그인 보장 백업)");
            }
        }
#endif
    }

    public void SetGuestMode(bool isGuest)
    {
        if (isGuest)
        {
            Debug.LogWarning($"{GetType()}::Guest Mode 입니다.");
            PlayerPrefs.SetString(PrefKeys.ScoreMode, "android");
            PlayerPrefs.Save();
        }
        else
        {
            Debug.Log($"{GetType()}::Guest Mode 아닙니다.");
            // 비-게스트: 기존 PlayerPrefs 값 유지 (사용자 설정 존중)
        }
    }

    // ────────────────────────────────────────────────────────────
    // Editor 테스트 도구 (Play Mode 에서 Inspector 드롭다운 선택 → 우클릭 Run)
    // #if UNITY_EDITOR 로 완전 격리 — Android/iOS 빌드에 0% 영향
    // ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    public enum LoginTestCase
    {
        [UnityEngine.InspectorName("── 실패: 앱 미설치 ──")]
        Fail_APP_NOT_INSTALLED,
        [UnityEngine.InspectorName("── 실패: 로그인 세션 없음 ──")]
        Fail_SESSION_NOT_READY,
        [UnityEngine.InspectorName("── 실패: PIN 인증 미완료 ──")]
        Fail_TOKEN_NOT_READY,
        [UnityEngine.InspectorName("── 실패: 메인앱 초기화 오류 ──")]
        Fail_PROVIDER_INIT_ERROR,
        [UnityEngine.InspectorName("── 실패: Provider 접근 예외 ──")]
        Fail_PROVIDER_EXCEPTION,
        [UnityEngine.InspectorName("── 실패: Provider 무응답 ──")]
        Fail_PROVIDER_NULL_CURSOR,
        [UnityEngine.InspectorName("── 실패: 전역 예외 ──")]
        Fail_PROVIDER_GLOBAL_EXCEPTION,
        [UnityEngine.InspectorName("── 실패: 파싱 실패 ──")]
        Fail_LOCAL_PARSE_FAIL,
        [UnityEngine.InspectorName("── 실패: 알 수 없는 응답 ──")]
        Fail_UNKNOWN_PARSE,
        [UnityEngine.InspectorName("── 실패: 최상위 예외 ──")]
        Fail_GET_DATA_EXCEPTION,
        [UnityEngine.InspectorName("── 성공: CAMI (로봇 등록) ──")]
        Success_CAMI,
        [UnityEngine.InspectorName("── 성공: CAMI (로봇 미등록) ──")]
        Success_CAMI_NoRobot,
        [UnityEngine.InspectorName("── 성공: Guest ──")]
        Success_Guest,
        [UnityEngine.InspectorName("── 모든 팝업 닫기 ──")]
        CloseAllPanels,
    }

    [Header("─── Editor Test (빌드 미포함) ───")]
    [SerializeField] private LoginTestCase editorTestCase = LoginTestCase.Fail_SESSION_NOT_READY;

    [ContextMenu("▶ Run Selected Test Case")]
    private void Test_RunSelected()
    {
        switch (editorTestCase)
        {
            case LoginTestCase.Fail_APP_NOT_INSTALLED:
                OpenFailPanel(null, false, "APP_NOT_INSTALLED");
                break;
            case LoginTestCase.Fail_SESSION_NOT_READY:
                OpenFailPanel("로봇 사용자 정보가 아직 준비되지 않았습니다.", true, "SESSION_NOT_READY");
                break;
            case LoginTestCase.Fail_TOKEN_NOT_READY:
                OpenFailPanel("PIN 인증이 완료되지 않았습니다.", true, "TOKEN_NOT_READY");
                break;
            case LoginTestCase.Fail_PROVIDER_INIT_ERROR:
                OpenFailPanel("Hilt 초기화 오류: TestContext", true, "PROVIDER_INIT_ERROR");
                break;
            case LoginTestCase.Fail_PROVIDER_EXCEPTION:
                OpenFailPanel(null, true, "PROVIDER_EXCEPTION");
                break;
            case LoginTestCase.Fail_PROVIDER_NULL_CURSOR:
                OpenFailPanel(null, true, "PROVIDER_NULL_CURSOR");
                break;
            case LoginTestCase.Fail_PROVIDER_GLOBAL_EXCEPTION:
                OpenFailPanel(null, true, "PROVIDER_GLOBAL_EXCEPTION");
                break;
            case LoginTestCase.Fail_LOCAL_PARSE_FAIL:
                OpenFailPanel(null, true, "LOCAL_PARSE_FAIL");
                break;
            case LoginTestCase.Fail_UNKNOWN_PARSE:
                OpenFailPanel(null, true, "UNKNOWN_PARSE");
                break;
            case LoginTestCase.Fail_GET_DATA_EXCEPTION:
                OpenFailPanel(null, false, "GET_DATA_EXCEPTION");
                break;
            case LoginTestCase.Success_CAMI:
                _userId = "test-user-id";
                _userName = "테스트유저";
                _robotId = "TEST-ROBOT-001";
                _prjId = "PRJ001";
                _prjName = "테스트 프로젝트";
                _gameAccessToken = "eyJtest...";
                SetPrefsAndOpenSuccess(PlatformType.CAMI);
                break;
            case LoginTestCase.Success_CAMI_NoRobot:
                _userId = "test-user-id";
                _userName = "테스트유저";
                _robotId = "";
                _prjId = "PRJ001";
                _prjName = "테스트 프로젝트";
                _gameAccessToken = "eyJtest...";
                SetPrefsAndOpenSuccess(PlatformType.CAMI);
                break;
            case LoginTestCase.Success_Guest:
                _userId = "guest";
                _userName = "guest";
                _robotId = "guest";
                SetPrefsAndOpenSuccess(PlatformType.Others);
                break;
            case LoginTestCase.CloseAllPanels:
                CloseFailPanel();
                if (loginSuccessPanel != null) loginSuccessPanel.ForceClose();
                if (loadingPanel != null) loadingPanel.StopLoading();
                break;
        }
    }
#endif
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
