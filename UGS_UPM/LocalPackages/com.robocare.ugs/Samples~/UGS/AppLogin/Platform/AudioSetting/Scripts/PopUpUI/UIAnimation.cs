using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    public static class UIAnimation
    {
        private static readonly Dictionary<UnityEngine.Object, Coroutine> RunningAnimations = new Dictionary<UnityEngine.Object, Coroutine>();
        private static AnimationRunner runner;

        private static AnimationRunner Runner
        {
            get
            {
                if (runner != null)
                    return runner;

                GameObject runnerObject = new GameObject(nameof(UIAnimation) + "Runner");
                runnerObject.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(runnerObject);
                runner = runnerObject.AddComponent<AnimationRunner>();
                return runner;
            }
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
            Color color = image.color;
            color.a = 0f;
            image.color = color;
            StartAnimation(image, FadeGraphicAlpha(image, 1f, duration, ignoreTimeScale));
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

        Color color = image.color;
        color.a = 1f;
        image.color = color;
        StartAnimation(image, FadeGraphicAlpha(image, 0f, duration, ignoreTimeScale));
    }

    public static void DoScaleIncrease(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
            return;

        gameObject.transform.localScale = Vector3.zero;
        StartAnimation(gameObject.transform, ScaleTransform(gameObject.transform, Vector3.one, duration, ignoreTimeScale));
    }

    public static void DoScaleIncrease(List<GameObject> gameObjects, float duration, bool ignoreTimeScale = false)
    {
        if (gameObjects == null)
            return;

        for (int i = 0; i < gameObjects.Count; i++)
        {
            DoScaleIncrease(gameObjects[i], duration, ignoreTimeScale);
        }
    }

    public static void DoScaleIncrease(TMP_Text text, float duration, bool ignoreTimeScale = false)
    {
        if (text == null)
            return;

        text.transform.localScale = Vector3.zero;
        StartAnimation(text.transform, ScaleTransform(text.transform, Vector3.one, duration, ignoreTimeScale));
    }

    public static void DoScaleDecrease(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
            return;

        gameObject.transform.localScale = Vector3.one;
        StartAnimation(gameObject.transform, ScaleTransform(gameObject.transform, Vector3.zero, duration, ignoreTimeScale));
    }

    public static void DoScaleDecrease(List<GameObject> gameObjects, float duration, bool ignoreTimeScale = false)
    {
        if (gameObjects == null)
            return;

        for (int i = 0; i < gameObjects.Count; i++)
        {
            DoScaleDecrease(gameObjects[i], duration, ignoreTimeScale);
        }
    }

    public static void DoScaleDecrease(TMP_Text text, float duration, bool ignoreTimeScale = false)
    {
        if (text == null)
            return;

        text.transform.localScale = Vector3.one;
        StartAnimation(text.transform, ScaleTransform(text.transform, Vector3.zero, duration, ignoreTimeScale));
    }

    public static void PopupShow(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        PopupShow(gameObject, duration, null, ignoreTimeScale);
    }

    public static void PopupShow(GameObject gameObject, float duration, Action callback, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
            return;

        gameObject.SetActive(true);

        float targetScale = GetPopupTargetScale(gameObject);
        gameObject.transform.localScale = Vector3.one * 0.2f;

        StartAnimation(gameObject.transform, PopupShowRoutine(gameObject, targetScale, duration, ignoreTimeScale, callback));
    }

    public static void PopupHide(GameObject gameObject, float duration, bool ignoreTimeScale = false)
    {
        PopupHide(gameObject, duration, null, ignoreTimeScale);
    }

    public static void PopupHide(GameObject gameObject, float duration, Action callback, bool ignoreTimeScale = false)
    {
        if (gameObject == null)
            return;

        StartAnimation(gameObject.transform, PopupHideRoutine(gameObject, duration, ignoreTimeScale, callback));
    }

    public static void DoText(TMP_Text tmp, string text, float duration, bool ignoreTimeScale = false)
    {
        if (tmp == null)
            return;

        StartAnimation(tmp, TextRoutine(tmp, text ?? string.Empty, duration, ignoreTimeScale));
    }

    private static void StartAnimation(UnityEngine.Object key, IEnumerator routine)
    {
        if (key == null)
            return;

        if (RunningAnimations.TryGetValue(key, out Coroutine previous) && previous != null)
            Runner.StopCoroutine(previous);

        RunningAnimations[key] = Runner.StartCoroutine(RunAndClear(key, routine));
    }

    private static IEnumerator RunAndClear(UnityEngine.Object key, IEnumerator routine)
    {
        yield return routine;
        RunningAnimations.Remove(key);
    }

    private static IEnumerator FadeGraphicAlpha(Graphic graphic, float endAlpha, float duration, bool ignoreTimeScale)
    {
        if (graphic == null)
            yield break;

        Color startColor = graphic.color;
        Color endColor = startColor;
        endColor.a = endAlpha;

        yield return Lerp(duration, ignoreTimeScale, t =>
        {
            if (graphic != null)
                graphic.color = Color.LerpUnclamped(startColor, endColor, EaseInOutQuad(t));
        });

        if (graphic != null)
            graphic.color = endColor;
    }

    private static IEnumerator ScaleTransform(Transform target, Vector3 endScale, float duration, bool ignoreTimeScale)
    {
        if (target == null)
            yield break;

        Vector3 startScale = target.localScale;

        yield return Lerp(duration, ignoreTimeScale, t =>
        {
            if (target != null)
                target.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
        });

        if (target != null)
            target.localScale = endScale;
    }

    private static IEnumerator PopupShowRoutine(GameObject gameObject, float targetScale, float duration, bool ignoreTimeScale, Action callback)
    {
        if (gameObject == null)
            yield break;

        Transform target = gameObject.transform;
        yield return ScaleTransform(target, Vector3.one * targetScale * 1.1f, duration, ignoreTimeScale);
        yield return ScaleTransform(target, Vector3.one * targetScale, duration, ignoreTimeScale);

        if (gameObject != null)
            callback?.Invoke();
    }

    private static IEnumerator PopupHideRoutine(GameObject gameObject, float duration, bool ignoreTimeScale, Action callback)
    {
        if (gameObject == null)
            yield break;

        Transform target = gameObject.transform;
        float currentScale = target.localScale.x;

        yield return ScaleTransform(target, Vector3.one * currentScale * 1.05f, duration, ignoreTimeScale);
        yield return ScaleTransform(target, Vector3.zero, duration, ignoreTimeScale);

        if (gameObject != null)
        {
            gameObject.SetActive(false);
            target.localScale = Vector3.one * 0.2f;
            callback?.Invoke();
        }
    }

    private static IEnumerator TextRoutine(TMP_Text tmp, string text, float duration, bool ignoreTimeScale)
    {
        if (tmp == null)
            yield break;

        tmp.text = string.Empty;

        yield return Lerp(duration, ignoreTimeScale, t =>
        {
            if (tmp == null)
                return;

            int length = Mathf.Clamp(Mathf.RoundToInt(text.Length * t), 0, text.Length);
            tmp.text = text.Substring(0, length);
        });

        if (tmp != null)
            tmp.text = text;
    }

    private static IEnumerator Lerp(float duration, bool ignoreTimeScale, Action<float> apply)
    {
        if (duration <= 0f)
        {
            apply?.Invoke(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            apply?.Invoke(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        apply?.Invoke(1f);
    }

    private static float GetPopupTargetScale(GameObject gameObject)
    {
        global::PopupScaler scaler = gameObject.GetComponent<global::PopupScaler>();
        return scaler != null ? scaler.CalculateScale() : 1f;
    }

    private static float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    private sealed class AnimationRunner : MonoBehaviour
    {
    }
}
}
