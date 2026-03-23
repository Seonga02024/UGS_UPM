namespace RoboCare.UGS
{
using UnityEngine;
using UnityEngine.Events;

public sealed class TermsGate : MonoBehaviour
{
    private const string Key_TosAcceptedVersion = PrefKeys.TosAcceptedVersion;

    [Header("약관 버전")]
    public int CURRENT_TOS_VERSION = 1;   // 모든 씬/프리팹 동일값 유지

    [Header("패널 루트")]
    public GameObject termsPanelRoot;

    [Header("이벤트")]
    public UnityEvent onTermsOpened;
    public UnityEvent onTermsClosed;

    [Header("모드 분기")]
    public bool forceAlwaysShowForQA = false;   // QA: 매 실행마다 강제 표시
    public bool resetOnStartForProd = false;    // Prod: 시작 시 1회 리셋

    // ===== 재진입 방지 플래그(프로세스 전역) =====
    private static bool s_IsShowing = false;
    private static bool s_StartedOnce = false;

    // 인스턴스 상태
    private bool _isOpen = false;

    public void CheckAndOpenIfNeeded(string reason = "external")
    {
        int before = PlayerPrefs.GetInt(Key_TosAcceptedVersion, 0);
        LogApi.Log($"[TOS] Start v={CURRENT_TOS_VERSION}, saved={before}, forceQA={forceAlwaysShowForQA}, resetFlag={resetOnStartForProd}, isShowing={s_IsShowing}");
        
        if (resetOnStartForProd)
        {
            PlayerPrefs.DeleteKey(Key_TosAcceptedVersion);
            PlayerPrefs.Save();
            resetOnStartForProd = false;
            LogApi.Log("[TOS] PlayerPrefs reset (CheckAndOpen)");
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
            LogApi.Log($"[TOS] Skip open (already showing). reason={reason}");
            return;
        }
        OpenTermsInternal(reason);
    }

    private void OpenTermsInternal(string reason)
    {
        if (!termsPanelRoot)
        {
            LogApi.LogWarning("[TOS] termsPanelRoot 미지정");
            onTermsClosed?.Invoke();
            return;
        }
        s_IsShowing = true;
        _isOpen = true;
        termsPanelRoot.SetActive(true);
        LogApi.Log($"[TOS] OPEN ({reason})");
        onTermsOpened?.Invoke();
    }

    public void AcceptAndClose()
    {
        PlayerPrefs.SetInt(Key_TosAcceptedVersion, CURRENT_TOS_VERSION);
        PlayerPrefs.SetString(PrefKeys.TosAcceptedAt, System.DateTime.UtcNow.ToString("o"));
        PlayerPrefs.SetString(PrefKeys.TosAcceptedAppVer, Application.version);
        PlayerPrefs.Save();

        if (termsPanelRoot) termsPanelRoot.SetActive(false);
        _isOpen = false;
        s_IsShowing = false; // 전역 락 해제
        LogApi.Log("[TOS] ACCEPT & CLOSE");
        onTermsClosed?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("Reset TOS (Editor)")]
    private void EditorResetTos()
    {
        PlayerPrefs.DeleteKey(Key_TosAcceptedVersion);
        PlayerPrefs.Save();
        LogApi.Log("[TOS] PlayerPrefs 리셋 완료");
    }
#endif
}

}
