using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    /*
     * 사용 방법:
     * 1) Remote Config에 announcement_message(string) 키를 생성합니다.
     * 2) LoginManager의 LoginCompleted 이벤트를 받으면 공지 Fetch를 시작합니다.
     * 3) announcementPanel/messageText/closeButton을 인스펙터에 연결합니다.
     */
    public class AnnouncementManager : MonoBehaviour
    {

        [Header("Optional UI")]
        [SerializeField] private GameObject announcementPanel;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button closeButton;
        [SerializeField] private float announcementVisibleSeconds = 3f;
        private const string AnnouncementKey = "announcement_message";

        private void Start()
        {
            if (LoginManager.Instance != null)
            {
                LoginManager.Instance.LoginCompleted += HandleUpdateCompleted;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() =>
                {
                    announcementPanel.SetActive(false);
                });
            }
            announcementPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (LoginManager.Instance != null)
            {
                LoginManager.Instance.LoginCompleted -= HandleUpdateCompleted;
            }
        }

        private void HandleUpdateCompleted()
        {
            _ = RunPostLoginDataSyncAsync();
        }

        private async Task RunPostLoginDataSyncAsync()
        {
            await CheckAndHandleUpdateAsync();
        }

        private async Task CheckAndHandleUpdateAsync()
        {
            try
            {
                await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

                string announcementMessage = RemoteConfigService.Instance.appConfig.GetString(AnnouncementKey);
                LogApi.Log("[AnnouncementManager] announcementMessage : " + announcementMessage);
                if (!string.IsNullOrEmpty(announcementMessage))
                {
                    if (messageText != null)
                    {
                        messageText.text = announcementMessage;
                        LogApi.Log("[AnnouncementManager] announcementMessage : " + announcementMessage);
                    }

                    announcementPanel.SetActive(true);
                    await Task.Delay(TimeSpan.FromSeconds(announcementVisibleSeconds));
                    announcementPanel.SetActive(false);
                }
            }
            catch (Exception e)
            {
                LogApi.LogWarning("[AnnouncementManager] Remote Config fetch failed. fallback values will be used. " + e.Message);
            }
        }

        private struct UserAttributes
        {
        }

        private struct AppAttributes
        {
        }
    }
}
