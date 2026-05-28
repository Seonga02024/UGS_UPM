using System;
using Newtonsoft.Json;

public enum LoginParseStatus
{
    Success,          // 유효 데이터 획득
    ErrorWithCode,    // JSON status=error (errorCode + message 존재)
    EmptyToken,       // 토큰이 비어있음 (로그인 안 됨)
    FormatMismatch,   // 파싱 실패 (underscore/JSON 모두 실패)
}

public readonly struct LoginParseOutcome
{
    public readonly LoginParseStatus Status;
    public readonly string ErrorCode;
    public readonly string Message;
    public readonly LoginResult Data;

    public LoginParseOutcome(LoginParseStatus s, LoginResult d = default, string code = null, string msg = null)
    {
        Status = s;
        Data = d;
        ErrorCode = code;
        Message = msg;
    }
}

/// <summary>
/// 메인앱이 넘긴 raw token 문자열을 파싱.
/// 1) JSON 우선 (신규 포맷: status/data/errorCode/message)
/// 2) 실패 시 레거시 underscore split (하위 호환)
/// </summary>
public static class LoginTokenParser
{
    public static LoginParseOutcome Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new LoginParseOutcome(LoginParseStatus.EmptyToken,
                msg: "받아온 로그인 정보가 비어 있습니다.\n\n케미 프렌즈 앱에서 다시 로그인 진행해주세요!");
        }

        if (TryParseJson(raw, out var jsonOutcome))
            return jsonOutcome;

        return ParseLegacy(raw);
    }

    // ── JSON ──
    private static bool TryParseJson(string raw, out LoginParseOutcome outcome)
    {
        outcome = default;

        // 빠른 선별: JSON은 '{'로 시작
        var trimmed = raw.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '{') return false;

        try
        {
            var payload = JsonConvert.DeserializeObject<LoginProviderPayload>(raw);
            if (payload == null || string.IsNullOrEmpty(payload.status)) return false;

            if (payload.status == "error")
            {
                outcome = new LoginParseOutcome(
                    LoginParseStatus.ErrorWithCode,
                    code: payload.errorCode,
                    msg: payload.message);
                return true;
            }

            if (payload.status == "success")
            {
                var d = payload.data;
                if (d == null || string.IsNullOrEmpty(d.robotUserId) || string.IsNullOrEmpty(d.userName))
                {
                    var missing = new System.Collections.Generic.List<string>();
                    if (d == null) { missing.Add("data 전체"); }
                    else
                    {
                        if (string.IsNullOrEmpty(d.robotUserId)) missing.Add("사용자 ID (robotUserId)");
                        if (string.IsNullOrEmpty(d.userName)) missing.Add("사용자 이름 (userName)");
                    }
                    string detail = string.Join(", ", missing);
                    outcome = new LoginParseOutcome(LoginParseStatus.EmptyToken,
                        code: "SESSION_NOT_READY",
                        msg: $"로그인 정보가 비어있습니다.<br><br>비어있는 값: {detail}");
                    return true;
                }

                outcome = new LoginParseOutcome(LoginParseStatus.Success, new LoginResult
                {
                    userId = d.robotUserId,
                    userName = d.userName,
                    robotId = d.robotId ?? "",
                    prjId = d.prjId ?? "",
                    prjName = d.prjNm ?? "",
                    gameAccessToken = d.accessToken ?? "",
                    fcmToken = d.fcmToken ?? "",
                    platform = PlatformType.CAMI,
                });
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ── 레거시 underscore 포맷 (기존 로직 보존) ──
    // userId_userName_robotId_prjId_prjNm_accessToken_fcmToken
    private static LoginParseOutcome ParseLegacy(string raw)
    {
        string[] parts = raw.Split('_');
        if (parts.Length < 3)
        {
            return new LoginParseOutcome(LoginParseStatus.FormatMismatch,
                msg: "케미프렌즈 앱에서 받아온 로그인 정보가 형식과 다릅니다.\n\n케미 프렌즈 앱에서 다시 로그인 진행해주세요!");
        }

        string userId = parts[0];
        string userName = parts[1];
        string robotId = parts[2];

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
        {
            var missing = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(userId)) missing.Add("사용자 ID");
            if (string.IsNullOrEmpty(userName)) missing.Add("사용자 이름");
            string detail = string.Join(", ", missing);
            return new LoginParseOutcome(LoginParseStatus.EmptyToken,
                msg: $"로그인 정보가 비어있습니다.<br><br>비어있는 값: {detail}");
        }

        string prjId = "", prjName = "", accessToken = "", fcmToken = "";

        // JWT는 항상 "eyJ"로 시작 → prjName 안에 '_' 포함 가능성 대응
        int jwtIndex = -1;
        for (int i = 3; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("eyJ")) { jwtIndex = i; break; }
        }

        if (jwtIndex >= 0)
        {
            if (jwtIndex > 3) prjId = parts[3];
            if (jwtIndex > 4) prjName = string.Join("_", parts, 4, jwtIndex - 4);

            // JWT+FCM 합쳐진 구간: FCM 은 '<id>:APA91...' 형태라 첫 ':' 앞 마지막 '_' 로 경계 판정
            string combined = string.Join("_", parts, jwtIndex, parts.Length - jwtIndex);
            int colonIdx = combined.IndexOf(':');
            if (colonIdx >= 0)
            {
                int sepIdx = combined.LastIndexOf('_', colonIdx);
                if (sepIdx >= 0)
                {
                    accessToken = combined.Substring(0, sepIdx);
                    fcmToken = combined.Substring(sepIdx + 1);
                }
                else
                {
                    accessToken = combined;
                }
            }
            else
            {
                accessToken = combined;
            }
        }
        else
        {
            // JWT 못 찾음 → 고정 인덱스 fallback
            if (parts.Length >= 4) prjId = parts[3];
            if (parts.Length >= 5) prjName = parts[4];
            if (parts.Length >= 6) accessToken = parts[5];
        }

        return new LoginParseOutcome(LoginParseStatus.Success, new LoginResult
        {
            userId = userId,
            userName = userName,
            robotId = robotId,
            prjId = prjId,
            prjName = prjName,
            gameAccessToken = accessToken,
            fcmToken = fcmToken,
            platform = PlatformType.CAMI,
        });
    }
}
