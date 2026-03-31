namespace RoboCare.UGS
{
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginSuccessPanel : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup panelGroup;          // 기존 loginSuccessCanvasGroup 연결
    [SerializeField] private TMP_Text robotIdText;            // RobotIdText
    [SerializeField] private TMP_Text loginSuccessInfoText;
    [SerializeField] private TMP_Text platformQuestionText;   // PlatformQuestionText
    [SerializeField] private Button yesButton;                // 예
    [SerializeField] private Button noButton;                 // 아니오
    [SerializeField] private TMP_Text autoHintText;           // "3초 뒤 자동으로 진행됩니다"

    [Header("Timing")]
    [SerializeField] private float autoDelaySeconds = 5f;     // 5초 후 자동 진행
    [SerializeField] private float fadeOutDuration = 0.8f;    // 페이드아웃 시간
    
    public Action OnConfirmPlatform;   // 예 또는 자동 확정 시 콜백
    public Action OnChangePlatform;    // 아니오 클릭 시 콜백 (플랫폼 선택 UI 열기 등)
    
    public event Action Shown;
    public event Action Hidden;
    public event Action FinishAppLogin;
    
    public Color color_bomi1 = new Color(0.07f, 0.81f, 0.92f);
    public Color color_cami = new Color(0.94f, 0.54f, 0.84f);
    public Color color_others = new Color(0.72f, 0.40f, 0.78f);
    
    public bool IsShowing => panelGroup && panelGroup.gameObject.activeInHierarchy && panelGroup.alpha > 0.01f;

    private Coroutine _routine;
    
    void Awake()
    {
        panelGroup.gameObject.SetActive(false);
        panelGroup.alpha = 0f;

        yesButton.onClick.AddListener(() =>
        {
            // 즉시 확정 + 페이드아웃
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FadeOutThenInvoke(confirm: true));
        });

        noButton.onClick.AddListener(() =>
        {
            ForceClose();
            OnChangePlatform?.Invoke();
        });
    }
    
    
    public void Show(string userName, string robotId, PlatformType platform)
    {
        // 텍스트 바인딩
        robotIdText.text = $"{robotId}";

        string userNameLabel = "";
        if (string.IsNullOrEmpty(userName) || userName == "unknown" || userName == "Unknown")
            userNameLabel = "";
        else
            userNameLabel = $"<size=130%>{userName}</size> 님 ";
        loginSuccessInfoText.text = $"{userNameLabel}로그인 성공하셨습니다.";

        string label = platform.ToString();
        Color platformColor = Color.white;
        switch (platform)
        {
            case PlatformType.BOMI1:
                label = "보미1";
                platformColor = color_bomi1;
                break;
            case PlatformType.BOMI2:
                label = "보미2";
                platformColor = Color.white;
                break;
            case PlatformType.CAMI:
                label = "케미";
                platformColor = color_cami;
                break;
            case PlatformType.Others:
                label = "로봇 없는 환경";
                platformColor = color_others;
                break;
        }
        string hex = ColorUtility.ToHtmlStringRGB(platformColor);
        platformQuestionText.text = $"현재 <color=#{hex}><size=120%><b>{label}</b></size></color>에서 플레이 중입니다." +
                                    $"<br>아니라면 '아니에요' 버튼을 눌러주세요.";
        
        autoHintText.text = $"{autoDelaySeconds:0}초 뒤 자동으로 진행됩니다";

        // 표시
        panelGroup.gameObject.SetActive(true);
        panelGroup.alpha = 1f;
        
        Shown?.Invoke();

        // 카운트다운 시작
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(AutoCloseAfterDelay());
    }

    private IEnumerator AutoCloseAfterDelay()
    {
        float remain = autoDelaySeconds;
        while (remain > 0f)
        {
            autoHintText.text = $"{Mathf.CeilToInt(remain)}초 뒤 자동으로 진행됩니다";
            remain -= Time.deltaTime;
            yield return null;
        }

        // 자동 확정 후 페이드아웃 시작
        yield return FadeOutThenInvoke(confirm: true);
    }

    private IEnumerator FadeOutThenInvoke(bool confirm)
    {
        float t = 0f;
        float start = panelGroup.alpha;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            panelGroup.alpha = Mathf.Lerp(start, 0f, t / fadeOutDuration);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelGroup.gameObject.SetActive(false);

        if (confirm)
        {
            Hidden?.Invoke();
            OnConfirmPlatform?.Invoke();
            FinishAppLogin?.Invoke();
            LogApi.Log("[LoginSuccessPanel] FinishAppLogin Invoke");
        }
        else
            OnChangePlatform?.Invoke();
    }

    public void ForceClose()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        panelGroup.alpha = 0f;
        panelGroup.gameObject.SetActive(false);
        Hidden?.Invoke();
    }
}

}
