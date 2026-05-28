/// <summary>
/// Login 성공 시 LoginTokenReader → Bridge 로 전달되는 결과 DTO.
/// </summary>
public struct LoginResult
{
    public string userId;
    public string userName;
    public string robotId;
    public string prjId;
    public string prjName;
    public string gameAccessToken;
    public string fcmToken;
    public PlatformType platform;
    public string sourcePackage;   // 매칭된 CAMI 패키지 (운영/QA/DEV)
    public bool isGuest;
}
