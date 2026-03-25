namespace RoboCare.UGS
{
using UnityEngine;
using System.Collections.Generic;
using System;

// 전체 퀘스트 목록 UI를 관리
public class QuestPanel : MonoBehaviour
{
    #region [Fields - UI & References]
    [Header("UI Components")]
    [SerializeField] private Transform questListContainer; // QuestLog_Panel의 Content 오브젝트 연결
    [SerializeField] private GameObject questUIPrefab;     // QuestItem_Prefab을 연결
    private List<GameObject> _instantiatedQuestItems = new List<GameObject>();
    #endregion
    
    // 이 함수를 외부(예: QuestManager)에서 호출하여 UI를 갱신합니다.
    /// <summary>
    /// DailyQuestManager 에서 할당한 퀘스트를 UI 로 표시 
    /// </summary>
    public void UpdateQuestUI(UserQuestStatus currentStatus)
    {
        // 기존 UI들을 모두 삭제하여 중복 생성을 방지합니다.
        foreach (GameObject item in _instantiatedQuestItems)
        {
            Destroy(item);
        }
        _instantiatedQuestItems.Clear();

        // 활성화된 모든 퀘스트에 대해 UI 아이템을 생성합니다.
        foreach (QuestProgress quest in currentStatus.activeQuests)
        {
            GameObject questItemInstance = Instantiate(questUIPrefab, questListContainer);
            questItemInstance.GetComponent<QuestUIItem>().SetQuest(quest);
            _instantiatedQuestItems.Add(questItemInstance);
        }
    }

    /// <summary>
    /// player 퀘스트를 완료하여 보상을 받을 수 있게 UI 처리 
    /// </summary>
    public void UpdateQuestProgress(QuestProgress quests)
    {
        Debug.Log($"퀘스트 완료하여 보상 버튼 활성화 진행");
        foreach (GameObject questItem in _instantiatedQuestItems)
        {
            QuestUIItem questItemSC = questItem.GetComponent<QuestUIItem>();
            if (questItemSC.questId == quests.id)
            {
                questItemSC.ChangeCompletedState(quests);
            }
        }
    }

    /// <summary>
    /// 현재 퀘스트 진행 정도 UI 업데이트 
    /// </summary>
    public void UpdateQuestUI(QuestProgress quests) // 퀘스트 완료 시 보상 버튼 활성화 
    {
        Debug.Log($"퀘스트 완료하여 보상 버튼 활성화 진행");
        foreach (GameObject questItem in _instantiatedQuestItems)
        {
            QuestUIItem questItemSC = questItem.GetComponent<QuestUIItem>();
            if (questItemSC.questId == quests.id)
            {
                questItemSC.UpdateUI(quests);
            }
        }
    }
}

[Serializable]
public class UserQuestStatus
{
    public string lastAssignDate = "";
    public List<QuestProgress> activeQuests = new List<QuestProgress>();
}
}
