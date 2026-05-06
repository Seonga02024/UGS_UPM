namespace RoboCare.UGS
{
using System.Collections.Generic;
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
        SendCompleteReward(_data.id);
    }
    
    // 퀘스트 완료 시, cloud code 를 통해 보상 지급 
    public void SendCompleteReward(string rewardId){
        if (string.IsNullOrWhiteSpace(rewardId))
        {
            Debug.LogError("[PlayerDataManager] SendCompleteReward failed: rewardId is empty.");
            return;
        }

        var req = new CompleteGameRewardRequest
        {
            REWARD_ID = rewardId
        };

        GameRewardsManager.Instance.CompleteGameReward(req, res =>
        {
            if (res == null)
            {
                Debug.LogError("[PlayerDataManager] SendCompleteReward callback is null.");
                return;
            }

            if (res.success)
            {
                completedBlock.SetActive(true);
                GameRewardsManager.Instance.ClaimRewardDirectlyToPrefs(_data.id);
                PlayerDataManager.Gold = (int)res.currentMoney;
                // 재화 UI 업데이트 따로 추가  

                if (PlayerDataManager.CurrentPlayerData != null)
                {
                    if (PlayerDataManager.CurrentPlayerData.completeGameRewards == null)
                    {
                        PlayerDataManager.CurrentPlayerData.completeGameRewards = new List<string>();
                    }

                    if (!PlayerDataManager.CurrentPlayerData.completeGameRewards.Contains(rewardId))
                    {
                        PlayerDataManager.CurrentPlayerData.completeGameRewards.Add(rewardId);
                    }
                }
            }

            Debug.Log($"[PlayerDataManager][CompleteGameReward Callback] success={res.success}, rewardId={res.rewardId}, reward={res.reward}, currentMoney={res.currentMoney}, error={res.errorCode}, message={res.message}");
        });
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
