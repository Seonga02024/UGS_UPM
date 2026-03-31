namespace RoboCare.UGS
{
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class LoginLoadingPanel : MonoBehaviour
{
    public GameObject loadingPanel;
    public CanvasGroup loadingCg;
    public float defaultLoadingTime = 3f; // 로그인이 성공해도 기본적으로 적용 될 로딩 화면
    
    Coroutine _co;
    bool _isShowing;

    void Awake() => ForceClose();

    public void ForceClose()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        _isShowing = false;
        if (loadingPanel) loadingPanel.SetActive(false);
        if (loadingCg) loadingCg.alpha = 0f;
    }

    public void StartLoading()
    {
        if (_isShowing) return;
        _co = StartCoroutine(ShowRoutine());
    } 

    IEnumerator ShowRoutine()
    {
        _isShowing = true;
        if (loadingPanel) loadingPanel.SetActive(true);
        if (loadingCg) loadingCg.alpha = 1f;
        yield break;
    }

    public void StopLoading() => ForceClose();

    public async UniTask ShowWhile(UniTask op, float? minSeconds = null)
    {
        // 켜기
        StartLoading();

        float minDur = Mathf.Max(0f, minSeconds ?? defaultLoadingTime);
        var minDelay = UniTask.Delay(TimeSpan.FromSeconds(minDur), DelayType.UnscaledDeltaTime);

        await UniTask.WhenAll(op, minDelay);

        // 닫기
        StopLoading();
    }
    
    public bool IsShowing => loadingPanel && loadingPanel.activeInHierarchy && loadingCg && loadingCg.alpha > 0.01f;
}

}
