namespace RoboCare.UGS
{
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using RoboCare.UGS;

// 단일 퀘스트의 전체 UI를 담당
public class QuestUIItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI questNameText;

    [Tooltip("완료했을 경우, 더 이상 상호작용 안되도록 해주는 패널")]
    [SerializeField] private GameObject completedBlock;
    [SerializeField] private TextMeshProUGUI goalText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private TextMeshProUGUI currentProgressText;
    [SerializeField] private Button getRewardBtn;
    [NonSerialized] public string questId = "";
    private QuestProgress _data = null;

    #region [Initialization & Setting]
    private void Start()
    {
        getRewardBtn.onClick.AddListener(CheckReward);
    }

    /// <summary>
    /// 보상 받기 버튼을 누른 후, 보상 받기 
    /// </summary>
    private void CheckReward()
    {
        if (_data.isCompleted) return;
        _data.isCompleted = true;
        completedBlock.SetActive(true);
        SendCompleteQuest(_data.id);
    }
    
    // 퀘스트 완료 시, cloud code 를 통해 보상 지급 
    public void SendCompleteQuest(string questId){
        if (string.IsNullOrWhiteSpace(questId))
        {
            Debug.LogError("[PlayerDataManager] SendCompleteQuest failed: questId is empty.");
            return;
        }

        var req = new CompleteQuestRewardRequest
        {
            QUEST_ID = questId
        };

        if (ServerEventManager.Instance == null)
        {
            Debug.LogError("[PlayerDataManager] SendCompleteQuest failed: ServerEventManager.Instance is null.");
            return;
        }

        DailyQuestManager.Instance.CompleteQuestReward(req, res =>
        {
            if (res == null)
            {
                Debug.LogError("[PlayerDataManager] SendCompleteQuest callback is null.");
                return;
            }

            if (res.success)
            {
                PlayerDataManager.Gold = (int)res.currentMoney;
                // 재화 UI 업데이트 따로 추가  
                DailyQuestManager.Instance.CompleteQuest(_data.id);
            }

            Debug.Log($"[PlayerDataManager][CompleteQuestReward Callback] success={res.success}, questId={res.questId}, reward={res.reward}, currentMoney={res.currentMoney}, error={res.errorCode}, message={res.message}");
        });
    }

    #endregion

        #region [Public Method]
        /// <summary>
        /// 처음에 기본 값 세팅 
        /// </summary>
        public void SetQuest(QuestProgress activeQuest)
        {
            _data = activeQuest;
            questId = _data.id;
            questNameText.text = _data.desc;
            goalText.text = _data.goal.ToString();
            rewardText.text = _data.reward.ToString();
            currentProgressText.text = _data.currentProgress.ToString();
            getRewardBtn.enabled = false;
            if (_data.isCompleted) completedBlock.SetActive(true);
            else completedBlock.SetActive(false);
        }

    /// <summary>
    /// 조건 달성하여 보상 받을 수 있는 상태 변화 
    /// </summary>
    public void ChangeCompletedState(QuestProgress activeQuest)
    {
        if (_data.isCompleted) return;

        _data = activeQuest;
        currentProgressText.text = _data.currentProgress.ToString();
        getRewardBtn.enabled = true;
    }

    /// <summary>
    /// 현재 퀘스트 진행 정도 UI 업데이트 
    /// </summary>
    public void UpdateUI(QuestProgress activeQuest)
    {
        _data = activeQuest;
        currentProgressText.text = _data.currentProgress.ToString();
    }
    #endregion
}

[Serializable]
public class QuestProgress
{
    public string id = "";
    public string desc = "";
    public string type = "";
    public int goal = 0;
    public int reward = 0;
    public int currentProgress = 0;
    public bool isCompleted = false;
}
}
