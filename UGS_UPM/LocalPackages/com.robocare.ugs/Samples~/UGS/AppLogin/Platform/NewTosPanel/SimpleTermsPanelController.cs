using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Simple TOS panel flow:
/// 1. User reads the loaded terms content.
/// 2. User presses Agree.
/// 3. "Start After Agree" becomes available.
/// 4. User presses Exit to close the app immediately.
/// </summary>
public sealed class SimpleTermsPanelController : MonoBehaviour
{
    private const string Key_TosAcceptedVersion = PrefKeys.TosAcceptedVersion;

    [Header("약관 버전")]
    public int CURRENT_TOS_VERSION = 1;   // 모든 씬/프리팹 동일값 유지

    [Header("이벤트")]
    public UnityEvent onTermsOpened;
    public UnityEvent onTermsClosed;

    [Header("패널 루트")]
    public GameObject termsPanelRoot;

    [Header("Buttons")]
    [SerializeField] private Button agreeButton;
    [SerializeField] private Button startAfterAgreeButton;
    [SerializeField] private Button exitButton;

    [Header("Scroll")]
    [SerializeField] private ScrollRect termsScrollRect;
    [SerializeField, Range(0f, 0.05f)] private float scrollBottomThreshold = 0.001f;
    [SerializeField] private float scrollToBottomDuration = 1f;

    [Header("Optional UI")]
    [SerializeField] private GameObject checkBtn;
    [SerializeField] private TMP_Text agreeStateText;
    [SerializeField] private string waitingText = "약관에 동의해 주세요.";
    [SerializeField] private string agreedText = "동의가 완료되었습니다.";

    [Header("모드 분기")]
    public bool forceAlwaysShowForQA = false;   // QA: 매 실행마다 강제 표시
    public bool resetOnStartForProd = false;    // Prod: 시작 시 1회 리셋

    // ===== 재진입 방지 플래그(프로세스 전역) =====
    private static bool s_IsShowing = false;
    private static bool s_StartedOnce = false;

    // 인스턴스 상태
    private bool _isOpen = false;

    private bool _agreed = false;
    private Coroutine _scrollCoroutine;
    private bool _isAutoScrolling = false;

    public void CheckAndOpenIfNeeded(string reason = "external")
    {
        int before = PlayerPrefs.GetInt(Key_TosAcceptedVersion, 0);
        Debug.Log($"[TOS] Start v={CURRENT_TOS_VERSION}, saved={before}, forceQA={forceAlwaysShowForQA}, resetFlag={resetOnStartForProd}, isShowing={s_IsShowing}");

        if (resetOnStartForProd)
        {
            PlayerPrefs.DeleteKey(Key_TosAcceptedVersion);
            PlayerPrefs.Save();
            resetOnStartForProd = false;
            Debug.Log("[TOS] PlayerPrefs reset (CheckAndOpen)");
        }

        if (forceAlwaysShowForQA)
        {
            TryOpenOnce(reason + " / force QA");
            return;
        }

        int accepted = PlayerPrefs.GetInt(Key_TosAcceptedVersion, 0);

        if (accepted < CURRENT_TOS_VERSION)
            TryOpenOnce(reason + " / version gate");
        else
            onTermsClosed?.Invoke();
    }

    private void TryOpenOnce(string reason)
    {
        if (s_IsShowing || _isOpen)
        {
            Debug.Log($"[TOS] Skip open (already showing). reason={reason}");
            return;
        }
        OpenTermsInternal(reason);
    }

    private void OnEnable()
    {
        if (agreeButton != null)
        {
            agreeButton.onClick.AddListener(OnAgreeClicked);
        }

        if (startAfterAgreeButton != null)
        {
            startAfterAgreeButton.onClick.AddListener(AcceptAndClose);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitClicked);
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        if (agreeButton != null)
        {
            agreeButton.onClick.RemoveListener(OnAgreeClicked);
        }

        if (startAfterAgreeButton != null)
        {
            startAfterAgreeButton.onClick.RemoveListener(AcceptAndClose);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
        }
    }

    private void OpenTermsInternal(string reason)
    {
        if (!termsPanelRoot)
        {
            Debug.LogWarning("[TOS] termsPanelRoot 미지정");
            onTermsClosed?.Invoke();
            return;
        }
        s_IsShowing = true;
        _isOpen = true;
        termsPanelRoot.SetActive(true);
        Debug.Log($"[TOS] OPEN ({reason})");
        onTermsOpened?.Invoke();
    }

    public void AcceptAndClose()
    {
        if (!_agreed)
        {
            return;
        }

        PlayerPrefs.SetInt(Key_TosAcceptedVersion, CURRENT_TOS_VERSION);
        PlayerPrefs.SetString(PrefKeys.TosAcceptedAt, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.SetString(PrefKeys.TosAcceptedAppVer, Application.version);
        PlayerPrefs.Save();

        if (termsPanelRoot) termsPanelRoot.SetActive(false);
        _isOpen = false;
        s_IsShowing = false; // 전역 락 해제
        Debug.Log("[TOS] ACCEPT & CLOSE");
        onTermsClosed?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Reset TOS (Editor)")]
    private void EditorResetTos()
    {
        PlayerPrefs.DeleteKey(Key_TosAcceptedVersion);
        PlayerPrefs.Save();
        Debug.Log("[TOS] PlayerPrefs 리셋 완료");
    }
#endif

    public void OnAgreeClicked()
    {
        if (_isAutoScrolling)
        {
            return;
        }

        if (_agreed)
        {
            RefreshUI();
            return;
        }

        if (!IsScrolledToBottom())
        {
            StartScrollToBottomAnimation();
            RefreshUI();
            return;
        }

        _agreed = true;
        RefreshUI();
    }

    public void OnExitClicked()
    {
        if (isShow)
        {
            termsPanelRoot.SetActive(false);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ResetAgreement()
    {
        _agreed = false;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (startAfterAgreeButton != null)
        {
            startAfterAgreeButton.interactable = _agreed;
        }

        if (agreeStateText != null)
        {
            agreeStateText.text = _agreed ? agreedText : waitingText;
        }

        if (checkBtn)
        {
            checkBtn.SetActive(_agreed);
        }
    }

    private bool IsScrolledToBottom()
    {
        if (termsScrollRect == null)
        {
            return true;
        }

        return termsScrollRect.verticalNormalizedPosition <= scrollBottomThreshold;
    }

    private void ScrollToBottom()
    {
        if (termsScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        termsScrollRect.verticalNormalizedPosition = 0f;
        termsScrollRect.StopMovement();
    }

    private void StartScrollToBottomAnimation()
    {
        if (termsScrollRect == null)
        {
            return;
        }

        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
        }

        _scrollCoroutine = StartCoroutine(ScrollToBottomCoroutine());
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        _isAutoScrolling = true;

        Canvas.ForceUpdateCanvases();
        float start = termsScrollRect.verticalNormalizedPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, scrollToBottomDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            termsScrollRect.verticalNormalizedPosition = Mathf.Lerp(start, 0f, eased);
            yield return null;
        }

        termsScrollRect.verticalNormalizedPosition = 0f;
        termsScrollRect.StopMovement();
        _isAutoScrolling = false;
        _scrollCoroutine = null;
    }

    private bool isShow = false;

    public void SettingJustShowPanel()
    {
        if (isShow == false)
        {
            exitButton.GetComponent<Image>().sprite = startAfterAgreeButton.GetComponent<Image>().sprite;
            agreeButton.enabled = false;
            startAfterAgreeButton.enabled = false;   
        }
        isShow = true;
    }
}
