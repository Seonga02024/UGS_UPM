namespace RoboCare.UGS
{
/// <summary>
/// PlayerPrefs 키 상수 모음.
/// 키 문자열은 절대 변경하지 말 것 — 기존 사용자 데이터와 호환 필요.
/// 향후 UGS 연동 시 이 클래스를 래핑하는 형태로 마이그레이션 가능.
/// </summary>
public static class PrefKeys
{
    // ── User ──
    public const string User         = "user";
    public const string UserName     = "username";
    public const string RobotId      = "robotid";
    public const string Uuid         = "uuid";

    // ── Project ──
    public const string PrjId        = "prjid";
    public const string PrjType      = "prjtype";
    public const string PrjName      = "prjName";   // LoginTokenReader에서 사용 (대소문자 주의)
    public const string Version      = "version";

    // ── Endpoints ──
    public const string GameApi      = "gameapi";
    public const string TtsApi       = "ttsapi";
    public const string RobotApi     = "robotapi";
    public const string Socket       = "socket";
    public const string Mjpeg        = "mjpeg";
    public const string RobotInteractionApi = "robotinteractionapi";

    // ── Flags ──
    public const string ScoreMode    = "scoremode";
    public const string SvpMode      = "svpmode";
    public const string GetUserData  = "getuserdata";
    public const string RobotType    = "robotType";
    public const string GameMode     = "gamemode";
    public const string LogMode      = "logmode";
    public const string TtsMode      = "ttsmode";
    public const string TtsApiMode   = "ttsapimode";

    // ── Timeouts ──
    public const string ConnectTimeout = "connecttimeout";
    public const string ReadTimeout    = "readtimeout";
    public const string WriteTimeout   = "writetimeout";

    // ── App State ──
    public const string AppLaunched    = "AppAlreadyLaunched";

    // ── Volume ──
    public const string VolumeMaster = "volume.master";
    public const string VolumeBgm = "volume.bgm";
    public const string VolumeSfx = "volume.sfx";
    public const string VolumeTts = "volume.tts";
    public const string IsMuted   = "volume.isMuted";

    // ── Audio ──
    public const string SelectedBgm = "audio.selectedBgm";

    // ── Terms of Service ──
    public const string TosAcceptedVersion = "tos.accepted.version";
    public const string TosAcceptedAt      = "tos.accepted.at";
    public const string TosAcceptedAppVer  = "tos.accepted.appver";
}

}
