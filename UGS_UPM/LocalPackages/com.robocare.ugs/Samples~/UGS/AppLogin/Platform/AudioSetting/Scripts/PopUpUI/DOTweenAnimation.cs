using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DOTweenAnimation
{
    private static Tween TweenGraphicAlpha(Graphic graphic, float endAlpha, float duration, bool ignoreTimeScale, Ease ease)
    {
        Color endColor = graphic.color;
        endColor.a = endAlpha;

        Tween tween = DOTween.To(() => graphic.color, value => graphic.color = value, endColor, duration).SetEase(ease);
        if (ignoreTimeScale)
            tween.SetUpdate(true);

        return tween;
    }

    public static void FadeIn(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
        {
            Debug.LogError("GameObject is null.");
            return;
        }

        gameObject.SetActive(true);

        Image[] images = gameObject.GetComponentsInChildren<Image>();
        if (images.Length == 0)
        {
            Debug.LogError("Image component not found on the GameObject.");
            return;
        }

        foreach (Image image in images)
        {
            Color newColor = image.color;
            newColor.a = 0f;
            image.color = newColor;
            TweenGraphicAlpha(image, 1f, duration, ignoreTimeScale, Ease.InOutQuad);
        }
    }

    public static void FadeOut(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
        {
            Debug.LogError("GameObject is null.");
            return;
        }

        gameObject.SetActive(true);

        Image image = gameObject.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogError("Image component not found on the GameObject.");
            return;
        }

        Color newColor = image.color;
        newColor.a = 1f;
        image.color = newColor;
        TweenGraphicAlpha(image, 0f, duration, ignoreTimeScale, Ease.InOutQuad);
    }

    public static void DoScaleIncrease(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        gameObject.transform.localScale = Vector3.zero;
        Tween tween = gameObject.transform.DOScale(Vector3.one, duration);

        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }

    public static void DoScaleIncrease(List<GameObject> gameObjects, float duration, bool ignoreTimeScale = false)
    {
        for (int i = 0; i < gameObjects.Count; i++)
        {
            gameObjects[i].transform.localScale = Vector3.zero;
            Tween tween = gameObjects[i].transform.DOScale(Vector3.one, duration);

            if (ignoreTimeScale)
                tween.SetUpdate(true);
        }
    }

    public static void DoScaleIncrease(TMP_Text text, float duration, bool ignoreTimeScale = false)
    {
        text.transform.localScale = Vector3.zero;
        Tween tween = text.transform.DOScale(Vector3.one, duration);

        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }

    public static void DoScaleDecrease(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        gameObject.transform.localScale = Vector3.one;
        Tween tween = gameObject.transform.DOScale(Vector3.zero, duration);

        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }

    public static void DoScaleDecrease(List<GameObject> gameObjects, float duration, bool ignoreTimeScale = false)
    {
        for (int i = 0; i < gameObjects.Count; i++)
        {
            gameObjects[i].transform.localScale = Vector3.one;
            Tween tween = gameObjects[i].transform.DOScale(Vector3.zero, duration);

            if (ignoreTimeScale)
                tween.SetUpdate(true);
        }
    }

    public static void DoScaleDecrease(TMP_Text text, float duration, bool ignoreTimeScale = false)
    {
        text.transform.localScale = Vector3.one;
        Tween tween = text.transform.DOScale(Vector3.zero, duration);

        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }

    public static void PopupShow(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
            return;

        gameObject.SetActive(true);

        float targetScale = 1f;
        PopupScaler scaler = gameObject.GetComponent<PopupScaler>();
        if (scaler != null)
            targetScale = scaler.CalculateScale();

        gameObject.transform.localScale = Vector3.one * 0.2f;

        Sequence seq = DOTween.Sequence();
        seq.Append(gameObject.transform.DOScale(targetScale * 1.1f, duration));
        seq.Append(gameObject.transform.DOScale(targetScale, duration));

        if (ignoreTimeScale)
            seq.SetUpdate(true);

        seq.Play().OnUpdate(() =>
        {
            if (gameObject == null)
                seq.Kill();
        });
    }

    public static void PopupShow(GameObject gameObject, float duration, Action callback, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
            return;

        gameObject.SetActive(true);

        float targetScale = 1f;
        PopupScaler scaler = gameObject.GetComponent<PopupScaler>();
        if (scaler != null)
            targetScale = scaler.CalculateScale();

        gameObject.transform.localScale = Vector3.one * 0.2f;

        Sequence seq = DOTween.Sequence();
        seq.Append(gameObject.transform.DOScale(targetScale * 1.1f, duration));
        seq.Append(gameObject.transform.DOScale(targetScale, duration));

        if (ignoreTimeScale)
            seq.SetUpdate(true);

        seq.Play().OnUpdate(() =>
        {
            if (gameObject == null)
                seq.Kill();
        }).OnComplete(() =>
        {
            if (gameObject != null)
                callback?.Invoke();
        });
    }

    public static void PopupHide(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        if (gameObject == null) return;

        float curScale = gameObject.transform.localScale.x;
        Sequence seq = DOTween.Sequence();
        seq.Append(gameObject.transform.DOScale(curScale * 1.05f, duration));
        seq.Append(gameObject.transform.DOScale(0f, duration));

        if (ignoreTimeScale)
            seq.SetUpdate(true);

        seq.Play().OnComplete(() =>
        {
            if (gameObject != null)
            {
                gameObject.SetActive(false);
                gameObject.transform.localScale = Vector3.one * 0.2f;
            }
        });
    }

    public static void PopupHide(GameObject gameObject, float duration, Action callback, bool ignoreTimeScale = false)
    {
        if (gameObject == null) return;

        float curScale = gameObject.transform.localScale.x;
        Sequence seq = DOTween.Sequence();
        seq.Append(gameObject.transform.DOScale(curScale * 1.05f, duration));
        seq.Append(gameObject.transform.DOScale(0f, duration));

        if (ignoreTimeScale)
            seq.SetUpdate(true);

        seq.Play().OnComplete(() =>
        {
            if (gameObject != null)
            {
                gameObject.SetActive(false);
                gameObject.transform.localScale = Vector3.one * 0.2f;
                callback?.Invoke();
            }
        });
    }

    public static void DoText(TMP_Text tmp, string text, float duration, bool ignoreTimeScale = false)
    {
        if (tmp == null)
            return;

        string safeText = text ?? string.Empty;
        tmp.text = string.Empty;

        Tween tween = DOVirtual.Int(0, safeText.Length, duration, value =>
        {
            tmp.text = safeText.Substring(0, value);
        });

        if (ignoreTimeScale)
            tween.SetUpdate(true);
    }
}
