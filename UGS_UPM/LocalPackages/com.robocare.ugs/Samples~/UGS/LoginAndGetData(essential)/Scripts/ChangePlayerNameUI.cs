using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    // Canvas ChangePlayerNameUI Prefab 에 붙이기 
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
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.ChangeNameCompleted += HandleChangeNameCompleted;
        }
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
                if (!PlayerNameValidator.IsValid(playerNameIF.text))
                {
                    Debug.LogWarning("[ChangePlayerName] 부적절한 단어가 포함되어 저장할 수 없습니다.");
                    playerNameText.text = "사용할 수 없는 이름입니다.";
                    return;
                }
                await PlayerDataManager.Instance.SavePlayerName(playerNameIF.text);
            });
        }
        if (deletePlayerBtn) deletePlayerBtn.onClick.AddListener(async () => await PlayerDataManager.Instance.DeletePlayer());
        if (CloseBtn) CloseBtn.onClick.AddListener(() =>
        {
            playerSettingPanel.SetActive(false);
        });
    }
    
    private void HandleChangeNameCompleted(){
        playerNameText.text = "현재 플레이어 이름 : " + PlayerDataManager.CurrentPlayerData.name;
    }
}
}
