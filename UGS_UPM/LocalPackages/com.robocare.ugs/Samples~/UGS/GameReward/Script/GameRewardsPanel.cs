namespace RoboCare.UGS
{
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameRewardsPanel : MonoBehaviour
{
    #region [Fields - UI & References]
    [Header("UI Components")]
    [SerializeField] private Transform rewardListContainer; // QuestLog_Panel의 Content 오브젝트 연결
    [SerializeField] private GameObject rewardUIPrefab;     // QuestItem_Prefab을 연결
    [SerializeField] private TextMeshProUGUI currentPlayerStarCountText;
    private List<GameObject> _instantiatedRewardItems = new List<GameObject>();
    #endregion

    /// <summary>
    /// GameRewardsManager 에서 받은 데이터를 적용하여 UI 로 표시 
    /// </summary>
    public void UpdateRewardLog(RewardDefinitions rewardDatas)
    {
        // 기존 UI들을 모두 삭제하여 중복 생성을 방지합니다.
        foreach (GameObject item in _instantiatedRewardItems)
        {
            Destroy(item);
        }
        _instantiatedRewardItems.Clear();

        // 활성화된 모든 퀘스트에 대해 UI 아이템을 생성합니다.
        foreach (RewardData reward in rewardDatas.stage_rewards)
        {
            GameObject rewardItemInstance = Instantiate(rewardUIPrefab, rewardListContainer);
            rewardItemInstance.GetComponent<RewardUIItem>().SetReward(reward);
            _instantiatedRewardItems.Add(rewardItemInstance);
        }
    }

    /// <summary>
    /// player 가 얻은 별의 갯수가 조건을 충적할 시 UI 업데이트 
    /// </summary>
    public void UpdateRewardUI(int currentStar)
    {
        Debug.Log($"현재 얻은 별 수에 따른 보상 활성화");
        currentPlayerStarCountText.text = "현재까지 얻은 별 : " + currentStar.ToString();
        foreach (GameObject questItem in _instantiatedRewardItems)
        {
            RewardUIItem rewardtItemSC = questItem.GetComponent<RewardUIItem>();
            if (rewardtItemSC.goal <= currentStar)
            {
                rewardtItemSC.UpdateUI();
            }
        }
    }
}

}
