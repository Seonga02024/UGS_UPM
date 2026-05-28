using Cysharp.Threading.Tasks;

/// <summary>
/// Login 모듈과 외부(앱 서비스) 사이의 경계.
/// Platform 모듈은 이 인터페이스를 통해서만 외부 시스템과 통신한다.
/// 프로젝트별로 LoginBridgeBase 를 상속받아 구현 후 씬에 배치.
/// </summary>
public interface ILoginBridge
{
    /// <summary>레거시 플랫폼(BOMI HTTP 등) 감지. 구현 안 하면 false 반환.</summary>
    UniTask<bool> TryDetectLegacyAsync(float timeoutSec);

    /// <summary>로그인 성공 직후 훅 — 토큰/플랫폼을 외부 서비스에 전파.</summary>
    void OnLoginSucceeded(LoginResult result);

    /// <summary>약관 게이트. 동의 완료 후 true 반환. 필요 없으면 바로 true.</summary>
    UniTask<bool> EnsureTermsAcceptedAsync(PlatformType platform);

    /// <summary>최종 진입 훅 (게임 시작 이벤트 발화 등).</summary>
    void OnLoginCompleted(LoginResult result);
}
