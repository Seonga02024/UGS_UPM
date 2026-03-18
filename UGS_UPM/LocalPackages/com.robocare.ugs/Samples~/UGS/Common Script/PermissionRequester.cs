using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Android;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class PermissionRequester : MonoBehaviour
{
    const float QuitDelaySeconds = 0.75f;

    readonly List<string> _permissionsToRequest = new List<string>();
    readonly HashSet<string> _pendingPermissions = new HashSet<string>();
    bool _waitingForBatteryOptimizationResult;
    bool _quitScheduled;

    void Start()
    {
    #if UNITY_ANDROID
        AddIfNeeded(Permission.Camera); // 카메라 
        AddIfNeeded("android.permission.RECORD_AUDIO"); // 오디오 - 녹화 
        RequestAllAtOnce();
    #endif
    }

    void AddIfNeeded(string perm)
    {
        if (!Permission.HasUserAuthorizedPermission(perm))
            _permissionsToRequest.Add(perm);
    }

    void RequestAllAtOnce()
    {
        var cb = new PermissionCallbacks();
        var permissions = _permissionsToRequest.Distinct().ToArray();

        if (permissions.Length == 0)
        {
            RequestIgnoreBatteryOptimizationsIfNeeded();
            return;
        }

        _pendingPermissions.Clear();
        foreach (var permission in permissions)
            _pendingPermissions.Add(permission);

        cb.PermissionGranted += OnPermissionResolved;
        cb.PermissionDenied += OnPermissionResolved;
        cb.PermissionDeniedAndDontAskAgain += OnPermissionResolved;

        Permission.RequestUserPermissions(permissions, cb);
    }

    void OnPermissionResolved(string permission)
    {
        if (!_pendingPermissions.Remove(permission)) return;

        if (_pendingPermissions.Count == 0)
            RequestIgnoreBatteryOptimizationsIfNeeded();
    }

    void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!hasFocus) return;
        if (!_waitingForBatteryOptimizationResult) return;

        _waitingForBatteryOptimizationResult = false;
        if (!IsIgnoringBatteryOptimizations()) return;
        if (_quitScheduled) return;

        _quitScheduled = true;
        StartCoroutine(QuitAfterDelay()); 
#endif
    }

    void RequestIgnoreBatteryOptimizationsIfNeeded()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (IsIgnoringBatteryOptimizations()) return;

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                var packageName = currentActivity.Call<string>("getPackageName");

                using (var intent = new AndroidJavaObject(
                           "android.content.Intent",
                           "android.settings.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var packageUri = uriClass.CallStatic<AndroidJavaObject>("parse", string.Format("package:{0}", packageName)))
                {
                    _waitingForBatteryOptimizationResult = true;
                    intent.Call<AndroidJavaObject>("setData", packageUri);
                    currentActivity.Call("startActivity", intent);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to request ignore battery optimizations: {ex.Message}");
        } 
#endif
    }

    bool IsIgnoringBatteryOptimizations()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var powerManager = currentActivity.Call<AndroidJavaObject>("getSystemService", "power"))
            {
                var packageName = currentActivity.Call<string>("getPackageName");
                return powerManager.Call<bool>("isIgnoringBatteryOptimizations", packageName);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to read battery optimization state: {ex.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSecondsRealtime(QuitDelaySeconds);
        Application.Quit();
    }
}
