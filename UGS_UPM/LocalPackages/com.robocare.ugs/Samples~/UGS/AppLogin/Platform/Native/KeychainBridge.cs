// ============================================================================
// KeychainBridge.cs
// ============================================================================
// Assets/Plugins/iOS/KeychainBridge.mm 의 C# wrapper.
// LoginTokenReader iOS 분기에서 호출.
//
// [동작]
//   Editor / Android 빌드: 모든 메서드가 안전하게 false / null 반환 (no-op)
//   iOS 실기기: P/Invoke 로 native 함수 호출
//
// [메모리 정책]
//   native 가 strdup 로 반환한 char* 는 Marshal.PtrToStringAnsi 로 복사 후
//   _BomphagoFree 로 즉시 해제. 호출자가 신경쓸 것 없음.
//
// [Group 이름 결합]
//   메인앱과 합의된 base group: "com.robocare.shared(.qa/.dev)"
//   런타임 결합: prefix("ABCD123456.") + base = "ABCD123456.com.robocare.shared"
//
// [관련 파일]
//   Assets/Plugins/iOS/KeychainBridge.mm    (native 구현)
//   Assets/Editor/iOSPostProcessBuild.cs    (entitlements 등록 — 같은 base group)
//   Assets/Platform/LoginTokenReader.cs     (호출처)
// ============================================================================

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class KeychainBridge
{
    // ──────────────────────────────────────────────
    // 메인앱 합의 상수 (안드로이드 SmartBotPackages 와 짝)
    //   - URL Scheme: canOpenURL 검색용
    //   - Keychain Group: SecItemCopyMatching 의 kSecAttrAccessGroup
    //   - Service / Account: Keychain item 식별 키 (단일 JSON 저장)
    //
    //   배열 인덱스 순서 = 우선순위 (PROD > QA > DEV)
    // ──────────────────────────────────────────────
    public static readonly string[] MainAppUrlSchemes =
    {
        "smartbot-ios",      // PROD
        "smartbot-ios-qa",   // QA
        "smartbot-ios-dev",  // DEV
    };

    public static readonly string[] KeychainGroupBases =
    {
        "com.robocare.shared",      // PROD
        "com.robocare.shared.qa",   // QA
        "com.robocare.shared.dev",  // DEV
    };

    public const string KeychainService = "SmartbotLogin";
    public const string KeychainAccount = "userInfo";

    public static string EnvironmentLabel(int index)
    {
        switch (index)
        {
            case 0: return "PROD";
            case 1: return "QA";
            case 2: return "DEV";
            default: return $"UNKNOWN({index})";
        }
    }

    // ──────────────────────────────────────────────
    // P/Invoke (iOS 실기기 빌드에서만 링크됨)
    // ──────────────────────────────────────────────
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool _BomphagoCanOpenURL(string scheme);

    [DllImport("__Internal")]
    private static extern IntPtr _BomphagoGetAppIdPrefix();

    [DllImport("__Internal")]
    private static extern IntPtr _BomphagoKeychainRead(string accessGroup, string service, string account);

    [DllImport("__Internal")]
    private static extern void _BomphagoFree(IntPtr ptr);
#endif

    // ──────────────────────────────────────────────
    // Public API — Editor / Android 안전 (no-op 반환)
    // ──────────────────────────────────────────────

    /// <summary>
    /// 메인앱 설치 검색 (canOpenURL).
    /// scheme 은 콜론/슬래시 제외 ("smartbot-ios").
    /// </summary>
    public static bool CanOpenURL(string scheme)
    {
        if (string.IsNullOrEmpty(scheme)) return false;
#if UNITY_IOS && !UNITY_EDITOR
        try { return _BomphagoCanOpenURL(scheme); }
        catch (Exception ex) { Debug.LogWarning($"[KeychainBridge] CanOpenURL exception: {ex.Message}"); return false; }
#else
        return false;
#endif
    }

    /// <summary>
    /// AppIdentifierPrefix ("TEAMID." 형태) 조회. 첫 호출 시 native 측에서 캐시.
    /// 실패 시 null.
    /// </summary>
    public static string GetAppIdPrefix()
    {
        if (_cachedPrefix != null) return _cachedPrefix;
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            IntPtr ptr = _BomphagoGetAppIdPrefix();
            if (ptr == IntPtr.Zero) return null;
            string result = Marshal.PtrToStringAnsi(ptr);
            _BomphagoFree(ptr);
            _cachedPrefix = result;
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KeychainBridge] GetAppIdPrefix exception: {ex.Message}");
            return null;
        }
#else
        return null;
#endif
    }
    private static string _cachedPrefix;

    /// <summary>
    /// base group ("com.robocare.shared") + prefix → 완전한 group 이름 조립.
    /// </summary>
    public static string BuildFullAccessGroup(string baseGroup)
    {
        if (string.IsNullOrEmpty(baseGroup)) return null;
        string prefix = GetAppIdPrefix();
        if (string.IsNullOrEmpty(prefix)) return null;
        return prefix + baseGroup;
    }

    /// <summary>
    /// 메인앱이 저장한 Keychain item 조회.
    /// baseGroup 은 prefix 없는 형태 ("com.robocare.shared") — 내부에서 결합.
    /// 반환: JSON 문자열 / 없거나 실패 시 null.
    /// </summary>
    public static string ReadKeychain(string baseGroup, string service, string account)
    {
        if (string.IsNullOrEmpty(baseGroup) || string.IsNullOrEmpty(service) || string.IsNullOrEmpty(account))
            return null;

#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            string fullGroup = BuildFullAccessGroup(baseGroup);
            if (string.IsNullOrEmpty(fullGroup)) return null;

            IntPtr ptr = _BomphagoKeychainRead(fullGroup, service, account);
            if (ptr == IntPtr.Zero) return null;

            string result = Marshal.PtrToStringAnsi(ptr);
            _BomphagoFree(ptr);
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KeychainBridge] ReadKeychain exception: {ex.Message}");
            return null;
        }
#else
        return null;
#endif
    }
}
