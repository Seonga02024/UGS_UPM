namespace RoboCare.UGS
{
using RoboCare.UGS;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardUIItem : MonoBehaviour
{
    #region [Fields - UI & References]
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI goalText;
    [Tooltip("완료했을 경우, 더 이상 상호작용 안되도록 해주는 패널")]
    [SerializeField] private GameObject completedBlock;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button getRewardBtn;
    private RewardData _data = null;
    private bool isCompleted = false;
    public int goal = 0;
    #endregion

    #region [Initialization & Setting]
    private void Start()
    {
        getRewardBtn.onClick.AddListener(CheckReward);
    }

    private void CheckReward()
    {
        if (isCompleted) return;
        isCompleted = true;
        PlayerDataManager.Instance.SendCompleteReward(_data.id);
        completedBlock.SetActive(true);
        GameRewardsManager.Instance.ClaimRewardDirectlyToPrefs(_data.id);
    }
    #endregion

    #region [Public Method]
    public void SetReward(RewardData rewardData)
    {
        _data = rewardData;
        goalText.text = "X " + _data.goal.ToString();
        goal = _data.goal;
        rewardText.text = "X " + _data.reward.ToString();

        if (PlayerDataManager.Instance.CheckCompleteGameRewards(_data.id))
        {
            completedBlock.SetActive(true);
            isCompleted = true;
        }
        else
        {
            completedBlock.SetActive(false);
            isCompleted = false;
            getRewardBtn.enabled = false;
        }
    }

    public void UpdateUI()
    {
        if (isCompleted) return;
        getRewardBtn.enabled = true;
    }
    #endregion
}

}
