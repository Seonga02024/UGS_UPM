namespace RoboCare.UGS
{
/// <summary>
/// 기본 서버 엔드포인트 URL.
/// Intent/PlayerPrefs로 오버라이드 가능한 초기값.
/// </summary>
public static class DefaultEndpoints
{
    public const string GameApi     = "https://api.hibomi.com/api";
    public const string GameApiDev  = "https://devapi.hibomi.com/api";
    public const string TtsApi      = "http://112.168.39.146:38090";
    public const string RobotApi    = "http://10.68.112.155:4001/";
    public const string Socket      = "10.68.112.155:4001";
    public const string Mjpeg       = "10.68.112.155:8080";
    public const string RobotInteractionApi = "http://localhost:4001/api/robot-event";
}

}
