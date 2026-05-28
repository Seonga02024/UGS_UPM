using UnityEngine;

/// <summary>
/// Login 모듈의 프로젝트 의존 설정 (ScriptableObject).
/// 패키지명/타임아웃/플랫폼 색/메시지 템플릿/Editor 치트 계정 등.
/// </summary>
[CreateAssetMenu(fileName = "LoginModuleConfig", menuName = "Config/LoginModuleConfig")]
public class LoginModuleConfig : ScriptableObject
{
    [Header("CAMI 패키지 (우선순위 순: 운영 → QA → DEV)")]
    public string[] camiPackages = new[]
    {
        "com.robocare.smartbot",
        "com.robocare.smartbot.qa",
        "com.robocare.smartbot.dev"
    };

    [Tooltip("Provider URI 포맷. {0} 위치에 패키지명이 채워진다.")]
    public string tokenUriFormat = "content://{0}.util.provider/userinfo";

    [Header("감지 리트라이")]
    public int maxDetectAttempts = 3;
    public float detectRetryIntervalSec = 0.15f;
    public float legacyDetectTimeoutSec = 2.5f;

    [Header("플랫폼 색상")]
    public Color bomi1Color  = new Color(0.07f, 0.81f, 0.92f);
    public Color camiColor   = new Color(0.94f, 0.54f, 0.84f);
    public Color othersColor = new Color(0.72f, 0.40f, 0.78f);

    [Header("실패 메시지 템플릿")]
    [TextArea] public string msgEmptyCamiSession =
        "케미프렌즈 앱에 로그인이 되어 있지 않습니다.\n\n케미 프렌즈 앱에서 로그인 후 다시 시도해주세요!";
    [TextArea] public string msgEmptyToken =
        "케미프렌즈 앱에서 받아온 로그인 정보가 비어 있습니다.\n\n케미 프렌즈 앱에서 다시 로그인 진행해주세요!";
    [TextArea] public string msgFormatMismatch =
        "케미프렌즈 앱에서 받아온 로그인 정보가 형식과 다릅니다.\n\n케미 프렌즈 앱에서 다시 로그인 진행해주세요!";
    [TextArea] public string msgAppMissing =
        "케미 프렌즈 앱이 없습니다.<br>케미 프렌즈 앱을 설치해주세요.<br>게스트 모드 선택 시 점수를 저장하지 않고 게임 진행이 가능합니다.";

    [Header("Editor 치트 계정 (Editor 에서만 덮어쓰기)")]
    public bool useEditorCheat = true;
    public string cheatUserId   = "282679f4-0646-4cf4-8441-2c12074d1868";
    public string cheatUserName = "정병훈";
    public string cheatRobotId  = "B10000-001";
    public string cheatPrjName  = "TEST-CONTENT";

    [Header("앱 수명주기")]
    [Tooltip("Android OnApplicationPause 시 강제 종료 (Kiosk 환경 전용). 일반적으로 false 권장.")]
    public bool quitOnPauseAndroid = false;
}
