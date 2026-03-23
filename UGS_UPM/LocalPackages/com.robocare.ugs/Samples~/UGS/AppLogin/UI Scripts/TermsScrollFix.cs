namespace RoboCare.UGS
{
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public sealed class TermsScrollFix : MonoBehaviour
{
    [Header("필수 참조")]
    public ScrollRect scrollRect;          // ScrollRect_Terms
    public RectTransform viewport;         // Viewport (RectMask2D + Image)
    public RectTransform content;          // Content (Y:1/1, Pivot:1)
    public TextMeshProUGUI termsText;      // Text (TMP)
    public Scrollbar verticalScrollbar;    // 세로 스크롤바

    [Header("텍스트 소스")]
    public TextAsset termsKo;

    [Header("패딩(px)")]
    public float leftRightPadding = 48f;   // 좌우 여백
    public float topBottomPadding = 24f;   // 상하 여백

    private void Awake()
    {
        // 스크롤바 상시표시 + 방향 통일
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.verticalScrollbar = verticalScrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        if (verticalScrollbar != null)
        {
            verticalScrollbar.gameObject.SetActive(true);
            verticalScrollbar.direction = Scrollbar.Direction.BottomToTop; // 상단=1, 하단=0
        }

        // 텍스트 주입 + 래핑 보장
        termsText.text = termsKo ? termsKo.text : "약관 텍스트가 없습니다.";
        termsText.enableWordWrapping = true;
        termsText.overflowMode = TextOverflowModes.Truncate;

        // 1프레임에 걸쳐 레이아웃 강제 계산
        StartCoroutine(RecalcAndSnap());
    }

    private IEnumerator RecalcAndSnap()
    {
        // 프레임 0: 현상 반영
        Canvas.ForceUpdateCanvases();
        yield return null;

        // 프레임 1: 가로폭 고정 -> preferredHeight 계산 -> Content 키우기
        FixWidthsAndHeights();
        Canvas.ForceUpdateCanvases();

        // 최상단으로 스냅(상단=1)
        scrollRect.verticalNormalizedPosition = 1f;

        // 일부 기기 보정: 한 프레임 뒤 한 번 더
        yield return null;
        FixWidthsAndHeights();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void FixWidthsAndHeights()
    {
        if (!viewport || !content || !termsText) return;

        var textRT = (RectTransform)termsText.transform;

        // Text는 상단 고정 + 가로 스트레치
        textRT.anchorMin = new Vector2(0f, 1f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.pivot    = new Vector2(0.5f, 1f);

        // 좌우 패딩 적용(스트레치 상태에서 offset으로)
        textRT.offsetMin = new Vector2(leftRightPadding * 0.5f, textRT.offsetMin.y);
        textRT.offsetMax = new Vector2(-leftRightPadding * 0.5f, textRT.offsetMax.y);

        // 뷰포트 가로폭 - 패딩 = 텍스트 가로폭
        float textWidth = Mathf.Max(0f, viewport.rect.width - leftRightPadding);

        // TMP에 폭 알려주고 선호 높이 산출
        termsText.ForceMeshUpdate();
        var pref = termsText.GetPreferredValues(termsText.text, textWidth, Mathf.Infinity);
        float preferredHeight = Mathf.Ceil(pref.y);

        // Text 높이 직접 설정 (Fitter 안 쓸 때)
        textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);

        // Content도 그만큼 크게 (상단 고정 상태)
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        float contentHeight = preferredHeight + topBottomPadding;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
    }
}

}
