using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    public class PopupNetworkError : MonoBehaviour
    {
        [SerializeField] Button button_connect;
        [SerializeField] Button button_close;

        void OnEnable()
        {
            if (button_connect != null)
            {
                button_connect.onClick.RemoveListener(on_click_connect);
                button_connect.onClick.AddListener(on_click_connect);
            }
            if (button_close != null)
            {
                button_close.onClick.RemoveListener(on_click_close);
                button_close.onClick.AddListener(on_click_close);
            }
        }

        void OnDisable()
        {
            if (button_connect != null)
                button_connect.onClick.RemoveListener(on_click_connect);
            if (button_close != null)
                button_close.onClick.RemoveListener(on_click_close);
        }

        /// <summary>안드로이드/iOS 네트워크 설정 화면으로 이동</summary>
        void on_click_connect()
        {
    #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.WIFI_SETTINGS"))
                {
                    using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                        .GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        activity.Call("startActivity", intent);
                    }
                }
            }
            catch (System.Exception e)
            {
                LogApi.LogError($"[NetworkErrorPopup] WiFi 설정 열기 실패: {e.Message}");
            }
    #elif UNITY_IOS && !UNITY_EDITOR
            Application.OpenURL("App-Prefs:root=WIFI");
    #else
            Debug.Log("[NetworkErrorPopup] Editor: 네트워크 설정 열기 시뮬레이션");
    #endif
        }

        /// <summary>팝업 닫기</summary>
        void on_click_close()
        {
            gameObject.SetActive(false);
            Application.Quit();
        }
    }
}
