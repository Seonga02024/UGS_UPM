using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ILoginBridge 의 MonoBehaviour 추상 베이스.
/// LoginTokenReader 가 Inspector 에서 이 타입으로 참조를 잡는다.
/// 프로젝트별 구현체가 이것을 상속해 필요한 콜백만 override.
/// </summary>
public abstract class LoginBridgeBase : MonoBehaviour, ILoginBridge
{
    public virtual UniTask<bool> TryDetectLegacyAsync(float timeoutSec) => UniTask.FromResult(false);
    public virtual void OnLoginSucceeded(LoginResult result) { }
    public virtual UniTask<bool> EnsureTermsAcceptedAsync(PlatformType platform) => UniTask.FromResult(true);
    public virtual void OnLoginCompleted(LoginResult result) { }
}
