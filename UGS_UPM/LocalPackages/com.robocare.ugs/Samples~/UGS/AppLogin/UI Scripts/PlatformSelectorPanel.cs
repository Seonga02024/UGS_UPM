namespace RoboCare.UGS
{
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlatformSelectorPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button bomi1Btn;
    // [SerializeField] private Button bomi2Btn; // 보미2 필요시 활성화
    [SerializeField] private Button camiBtn;
    [SerializeField] private Button othersBtn;

    private Action<PlatformType> _onSelected;
    
    public event Action Opened;
    public event Action Closed;
    
    public bool IsOpen => gameObject.activeInHierarchy && group && group.alpha > 0.01f;

    void Awake()
    {
        gameObject.SetActive(false);
        if (group != null) group.alpha = 0f;

        bomi1Btn.onClick.AddListener(() => Select(PlatformType.BOMI1));
        // bomi2Btn.onClick.AddListener(() => Select(PlatformType.BOMI2));
        camiBtn.onClick.AddListener(() => Select(PlatformType.CAMI));
        othersBtn.onClick.AddListener(() => Select(PlatformType.Others));
    }

    public void Open(PlatformType current, Action<PlatformType> onSelected)
    {
        _onSelected = onSelected;
        if (titleText != null) titleText.text = $"현재 설정된 환경 : <b>{current}</b>\n올바른 환경을 선택하세요.";
        gameObject.SetActive(true);
        if (group != null) group.alpha = 1f;
        Opened?.Invoke();
    }

    private void Select(PlatformType type)
    {
        _onSelected?.Invoke(type);
        Close();
    }

    public void Close()
    {
        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
        Closed?.Invoke();
    }
}
}
