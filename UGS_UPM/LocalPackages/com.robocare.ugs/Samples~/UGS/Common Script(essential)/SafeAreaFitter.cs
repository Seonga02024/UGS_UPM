using UnityEngine;

namespace RoboCare.UGS
{
/// <summary>
/// SafeArea 대응: Screen.safeArea에 맞춰 RectTransform 앵커를 조정.
/// Canvas 직접 하위 패널에 부착하여 노치/Dynamic Island/홈 인디케이터 영역을 회피.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Header("추가 패딩 (실제 픽셀 단위)")]
    [Tooltip("SafeArea 영역에서 상단을 추가로 차감")]
    public float extraTop;
    [Tooltip("SafeArea 영역에서 하단을 추가로 차감 (예: 씬 내 하단 버튼 영역 회피)")]
    public float extraBottom;
    [Tooltip("SafeArea 영역에서 좌측을 추가로 차감")]
    public float extraLeft;
    [Tooltip("SafeArea 영역에서 우측을 추가로 차감")]
    public float extraRight;

    RectTransform rectTransform;
    Rect lastSafeArea;
    Vector4 lastExtraPadding;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        Vector4 currentExtra = new Vector4(extraTop, extraBottom, extraLeft, extraRight);
        if (lastSafeArea != Screen.safeArea || lastExtraPadding != currentExtra)
            ApplySafeArea();
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastExtraPadding = new Vector4(extraTop, extraBottom, extraLeft, extraRight);

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x += extraLeft;
        anchorMin.y += extraBottom;
        anchorMax.x -= extraRight;
        anchorMax.y -= extraTop;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}

}
