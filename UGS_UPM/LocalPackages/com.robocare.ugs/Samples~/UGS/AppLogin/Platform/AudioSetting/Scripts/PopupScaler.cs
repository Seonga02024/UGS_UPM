using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class PopupScaler : MonoBehaviour
{
    public enum FitMode
    {
        FitToTarget,
        ShrinkOnly,
        HeightOnly,
    }

    [Tooltip("FitToTarget: fit by width/height ratio\nShrinkOnly: only shrink when needed\nHeightOnly: fit by height only")]
    public FitMode mode = FitMode.FitToTarget;

    [Tooltip("Parent height ratio limit (0.9 = 90%)")]
    [Range(0.3f, 1f)]
    public float maxHeightRatio = 0.9f;

    [Tooltip("Parent width ratio limit (0.95 = 95%)")]
    [Range(0.3f, 1f)]
    public float maxWidthRatio = 0.95f;

    private RectTransform rectTransform;
    private RectTransform parentRect;

    void OnEnable()
    {
        CacheReferences();
        AdjustScale();
    }

    void Start()
    {
        CacheReferences();
        AdjustScale();
    }

    void OnRectTransformDimensionsChange()
    {
        CacheReferences();
        AdjustScale();
    }

    public float CalculateScale()
    {
        CacheReferences();
        if (parentRect == null || rectTransform == null) return 1f;

        float parentHeight = parentRect.rect.height;
        float parentWidth = parentRect.rect.width;
        float popupHeight = rectTransform.sizeDelta.y;
        float popupWidth = rectTransform.sizeDelta.x;

        if (popupHeight <= 0f || popupWidth <= 0f) return 1f;
        if (parentHeight <= 0f || parentWidth <= 0f) return 1f;

        float scaleByHeight = (parentHeight * maxHeightRatio) / popupHeight;
        float scaleByWidth = (parentWidth * maxWidthRatio) / popupWidth;

        if (mode == FitMode.FitToTarget)
            return Mathf.Min(scaleByHeight, scaleByWidth);

        if (mode == FitMode.HeightOnly)
            return scaleByHeight;

        float scale = 1f;
        if (popupHeight > parentHeight * maxHeightRatio) scale = scaleByHeight;
        if (popupWidth * scale > parentWidth * maxWidthRatio) scale = scaleByWidth;
        return scale;
    }

    private void AdjustScale()
    {
        float scale = CalculateScale();
        if (scale <= 0f) return;

        rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        parentRect = transform.parent as RectTransform;
    }
}
