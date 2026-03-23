namespace RoboCare.UGS
{
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 이용약관 UI 컨트롤러
/// - 약관 2개: 각각 끝까지 스크롤해야 체크박스가 활성화됨
/// - 두 체크박스 모두 체크해야 "동의" 버튼이 활성화됨
/// </summary>
public sealed class TermsUIController : MonoBehaviour
{
    [Header("UI")]
    public ScrollRect scrollRect1;   // 약관1 스크롤 영역
    public ScrollRect scrollRect2;   // 약관2 스크롤 영역
    public Button acceptButton;      // "동의" 버튼
    public Toggle agreeToggle1;      // 약관1 동의 체크박스
    public Toggle agreeToggle2;      // 약관2 동의 체크박스

    [Header("게이트")]
    public TermsGate gate;           // TermsGate 참조 (Accept 호출용)

    private bool _scrolled1ToBottom; // 약관1 끝까지 내렸는지 여부
    private bool _scrolled2ToBottom; // 약관2 끝까지 내렸는지 여부

    private void Awake()
    {
        _scrolled1ToBottom = false;
        _scrolled2ToBottom = false;

        // 처음엔 버튼 비활성화
        SetAccept(false);
        ResetValue();

        // 처음엔 체크박스도 비활성화 (스크롤 다 내리기 전까지 못 누름)
        if (agreeToggle1 != null) agreeToggle1.interactable = false;
        if (agreeToggle2 != null) agreeToggle2.interactable = false;

        // 스크롤 이벤트 등록
        scrollRect1.onValueChanged.AddListener(_ => CheckBottom1());
        scrollRect2.onValueChanged.AddListener(_ => CheckBottom2());

        // 체크박스 상태 변화 이벤트 등록
        if (agreeToggle1 != null)
            agreeToggle1.onValueChanged.AddListener(_ => Refresh());
        if (agreeToggle2 != null)
            agreeToggle2.onValueChanged.AddListener(_ => Refresh());

        // "동의" 버튼 클릭 시 처리
        acceptButton.onClick.AddListener(() =>
        {
            gate?.AcceptAndClose();
        });
    }

    /// <summary>
    /// 시작 전에 전부 초기화 한 번
    /// </summary>
    private void ResetValue()
    {
        if (agreeToggle1)
        {
            agreeToggle1.interactable = false;
            agreeToggle1.SetIsOnWithoutNotify(false);
        }
        if (agreeToggle2)
        {
            agreeToggle2.interactable = false;
            agreeToggle2.SetIsOnWithoutNotify(false);
        }
        acceptButton.interactable = false;
    }

    /// <summary>
    /// 약관1 스크롤 확인 → 끝까지 내리면 체크박스 활성화
    /// </summary>
    private void CheckBottom1()
    {
        _scrolled1ToBottom = scrollRect1.verticalNormalizedPosition <= 0.001f;
        if (agreeToggle1 != null)
            agreeToggle1.interactable = _scrolled1ToBottom;
        Refresh();
    }

    /// <summary>
    /// 약관2 스크롤 확인 → 끝까지 내리면 체크박스 활성화
    /// </summary>
    private void CheckBottom2()
    {
        _scrolled2ToBottom = scrollRect2.verticalNormalizedPosition <= 0.001f;
        if (agreeToggle2 != null)
            agreeToggle2.interactable = _scrolled2ToBottom;
        Refresh();
    }

    /// <summary>
    /// 스크롤/체크박스 조건 종합해서 버튼 상태 갱신
    /// </summary>
    private void Refresh()
    {
        bool agreeOk1 = (agreeToggle1 != null) && agreeToggle1.isOn;
        bool agreeOk2 = (agreeToggle2 != null) && agreeToggle2.isOn;

        // 두 체크박스가 모두 체크된 경우만 버튼 활성화
        SetAccept(agreeOk1 && agreeOk2);
    }

    /// <summary>
    /// 버튼 활성화 여부 변경
    /// </summary>
    private void SetAccept(bool on) => acceptButton.interactable = on;
}

}
