using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public static class BuildInfo
{
    public static string GetDisplayVersion()
    {
        // 기본은 Application.version
        string verName = Application.version;

#if UNITY_ANDROID && !UNITY_EDITOR
        long verCode = GetAndroidVersionCode();
        return $"{verName}_{verCode}";
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS: CFBundleVersion (Android versionCode 동등) — Native bridge 로 조회
        string verCode = GetIOSBuildNumber();
        return string.IsNullOrEmpty(verCode) ? verName : $"{verName}_{verCode}";
#else
        // 에디터/다른 플랫폼은 코드 없음
        return verName;
#endif
    }

    public static int GetAndroidVersionCode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
        {
            string packageName = currentActivity.Call<string>("getPackageName");
            using (AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0))
            {
                // Android P (API 28) 이상에서는 getLongVersionCode()를 권장하지만,
                // 일반적인 int 범위 내에서는 "versionCode" 필드로도 충분합니다.
                return packageInfo.Get<int>("versionCode");
            }
        }
    }
    catch (System.Exception e)
    {
        LogApi.LogWarning("버전 코드 로드 실패: " + e.Message);
        return 0;
    }
#else
        return 0; // 에디터나 다른 플랫폼일 경우
#endif
    }

    // ────────────────────────────────────────────────────────────
    // iOS — CFBundleVersion 조회 (Native bridge: KeychainBridge.mm)
    //
    // PlayerSettings → iOS → Identification → Build 또는
    // Xcode General → Identity → Build 값이 NSBundle.infoDictionary 의
    // CFBundleVersion 으로 들어감.
    // ────────────────────────────────────────────────────────────
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern System.IntPtr _BomphagoGetBuildNumber();

    [DllImport("__Internal")]
    private static extern void _BomphagoFree(System.IntPtr ptr);

    public static string GetIOSBuildNumber()
    {
        try
        {
            System.IntPtr ptr = _BomphagoGetBuildNumber();
            if (ptr == System.IntPtr.Zero) return null;
            string result = Marshal.PtrToStringAnsi(ptr);
            _BomphagoFree(ptr);
            return result;
        }
        catch (System.Exception e)
        {
            LogApi.LogWarning("iOS Build Number 로드 실패: " + e.Message);
            return null;
        }
    }
#else
    public static string GetIOSBuildNumber() => null;
#endif
}