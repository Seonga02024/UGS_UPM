using System;

/// <summary>
/// 메인앱(케미프렌즈)이 ContentProvider로 넘기는 JSON payload.
/// status == "success" 이면 data 사용, status == "error" 이면 errorCode/message 사용.
/// </summary>
[Serializable]
public class LoginProviderPayload
{
    public string status;            // "success" | "error"
    public LoginProviderData data;   // success
    public string errorCode;         // error: SESSION_NOT_READY | TOKEN_NOT_READY | PROVIDER_INIT_ERROR
    public string message;           // error: 사용자에게 보여줄 메시지
}

[Serializable]
public class LoginProviderData
{
    public string robotUserId;
    public string userName;
    public string robotId;
    public string prjId;
    public string prjNm;
    public string accessToken;
    public string fcmToken;
}
