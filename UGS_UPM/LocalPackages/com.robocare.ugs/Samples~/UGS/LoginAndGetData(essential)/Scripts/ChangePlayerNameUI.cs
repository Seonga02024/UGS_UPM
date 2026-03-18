using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChangePlayerNameUI : MonoBehaviour
{
    [SerializeField] private PlayerDataManager playerDataManager;

    #region [UI Components]
    [Header("UI Components")]
    [SerializeField] private Button playerSettingBtn;
    [SerializeField] private Button savePlayerBtn;
    [SerializeField] private Button deletePlayerBtn;
    [SerializeField] private Button CloseBtn;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_InputField playerNameIF;
    [SerializeField] private GameObject playerSettingPanel;
    #endregion

    private void Start()
    {
        playerSettingPanel.SetActive(false);
        if (playerDataManager != null)
        {
            playerDataManager.GetDataCompleted += HandleGetDataCompleted;
        }
    }

    private void HandleGetDataCompleted()
    {
        if (playerSettingBtn) playerSettingBtn.onClick.AddListener(() =>
            {
                playerNameText.text = "현재 플레이어 이름 : " + PlayerDataManager.CurrentPlayerData.name.Split('#')[0];
                playerSettingPanel.SetActive(true);
            }
        );
        if (savePlayerBtn)
        {
            savePlayerBtn.onClick.AddListener(async () =>
            {
                await PlayerDataManager.Instance.SavePlayerName(playerNameIF.text);
                playerNameText.text = "현재 플레이어 이름 : " + playerNameIF.text;
            });
        }
        if (deletePlayerBtn) deletePlayerBtn.onClick.AddListener(async () => await PlayerDataManager.Instance.DeletePlayer());
        if (CloseBtn) CloseBtn.onClick.AddListener(() => playerSettingPanel.SetActive(false));
    }
}
