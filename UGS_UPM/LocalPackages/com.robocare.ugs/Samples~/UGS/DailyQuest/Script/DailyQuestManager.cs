namespace RoboCare.UGS
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 퀘스트의 할당, 진행도 기록, 보상 상태를 관리하는 매니저
/// </summary>
public class DailyQuestManager : MonoBehaviour
{
    public static DailyQuestManager Instance { get; private set; }

    #region [Fields - UI & References]
    [Header("UI Components")]
    [SerializeField] private QuestPanel questPanel;
    [SerializeField] private Button questBtn;
    [SerializeField] private Button closeBtn;

    [Header("Data Status")]
    [SerializeField] private UserQuestStatus currentStatus;
    #endregion

    #region [Quest Data]
    private int dailyQuestCount = 2;
    #endregion

    #region [Unity Lifecycle]
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // 1. 로컬 데이터 먼저 로드
        LoadLocalData();
        
        // 2. 서비스 초기화 및 퀘스트 할당 (비동기)
        await CheckAndAssignQuests();

        // 3. 버튼 이벤트 바인딩
        questBtn.onClick.AddListener(() => OpenPanel(true));
        closeBtn.onClick.AddListener(() => OpenPanel(false));
    }
    #endregion

    #region [Initialization & Remote Config]
    /// <summary>
    /// Remote Config를 통해 최신 퀘스트 정의를 가져오고 오늘 자 퀘스트를 할당함
    /// </summary>
    private async Task CheckAndAssignQuests()
    {
        await InitializeRemoteConfig();

        string json = RemoteConfigService.Instance.appConfig.GetJson("QUEST_DEFINITIONS");
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[Quest] Remote Config 'QUEST_DEFINITIONS' is Null or Empty");
            return;
        }

        var definitions = JsonConvert.DeserializeObject<QuestDefinitions>(json);
        if (definitions?.daily_quests == null || definitions.daily_quests.Count == 0)
        {
            Debug.LogError("[Quest] Failed to parse QuestDefinitions or list is empty");
            return;
        }

        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // 날짜 비교 후 할당 혹은 데이터 동기화
        if (string.IsNullOrEmpty(currentStatus.lastAssignDate) || currentStatus.lastAssignDate != today)
        {
            AssignNewQuests(today, definitions);
        }
        else
        {
            UpdateQuestData(definitions);
        }
        
        // UI 초기화
        questPanel.UpdateQuestUI(currentStatus);
        OpenPanel(false);

        // 오늘 첫 접속 시 출석 체크 진행도 업데이트
        UpdateProgress(QuestType.TodayCheck, 1);
    }

    private async Task InitializeRemoteConfig()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // 인자값으로 빈 구조체 전달
        await RemoteConfigService.Instance.FetchConfigsAsync(new userAttributes(), new appAttributes());
        Debug.Log("[Quest] Remote Config Fetch Completed");
    }
    #endregion

    #region [Quest Management]
    /// <summary>
    /// 새로운 일일 퀘스트 2개를 무작위로 선택하여 할당
    /// </summary>
    private void AssignNewQuests(string date, QuestDefinitions definitions)
    {
        var selected = definitions.daily_quests
            .OrderBy(x => Guid.NewGuid())
            .Take(dailyQuestCount)
            .ToList();

        currentStatus.lastAssignDate = date;
        currentStatus.activeQuests.Clear();

        foreach (var q in selected)
        {
            currentStatus.activeQuests.Add(new QuestProgress
            {
                id = q.id,
                desc = q.desc,
                type = q.type,
                goal = q.goal,
                reward = q.reward,
                currentProgress = 0,
                isCompleted = false
            });
        }

        SaveQuestData();
        Debug.Log($"[Quest] New quests assigned for {date}");
    }

    /// <summary>
    /// 기존 할당된 퀘스트의 텍스트나 목표치가 변경되었을 경우 최신 정보로 갱신
    /// </summary>
    private void UpdateQuestData(QuestDefinitions definitions)
    {
        if (definitions?.daily_quests == null || currentStatus?.activeQuests == null) return;

        foreach (var q in currentStatus.activeQuests)
        {
            var originQ = definitions.daily_quests.Find(x => x.id == q.id);
            if (originQ != null)
            {
                q.desc = originQ.desc;
                q.type = originQ.type;
                q.goal = originQ.goal;
                q.reward = originQ.reward;
            }
        }
        SaveQuestData();
    }

    /// <summary>
    /// 특정 타입의 퀘스트 진행도를 추가함
    /// </summary>
    public void UpdateProgress(QuestType type, int amount)
    {
        bool isChanged = false;
        foreach (var q in currentStatus.activeQuests)
        {
            if (Enum.TryParse(q.type, out QuestType parsedType))
            {
                if (parsedType == type && !q.isCompleted)
                {
                    q.currentProgress += amount;
                    isChanged = true;

                    // 완료 여부에 따른 UI 연출 분기
                    if (q.currentProgress >= q.goal)
                    {
                        q.currentProgress = q.goal; // 초과 방지
                        questPanel.UpdateQuestProgress(q);
                    }
                    else
                    {
                        questPanel.UpdateQuestUI(q);
                    }
                }
            }
        }

        if (isChanged) SaveQuestData();
    }

    /// <summary>
    /// 유저가 보상을 수령했을 때 호출하여 완료 처리
    /// </summary>
    public void CompleteQuest(string questId)
    {
        var quest = currentStatus.activeQuests.FirstOrDefault(x => x.id == questId);
        if (quest != null)
        {
            quest.isCompleted = true;
            SaveQuestData();
            Debug.Log($"[Quest] {questId} marked as completed and saved.");
        }
    }
    #endregion

    #region [Data Persistence]
    private void SaveQuestData()
    {
        string json = JsonConvert.SerializeObject(currentStatus);
        PlayerPrefs.SetString("UserQuestStatus", json);
        PlayerPrefs.Save();
        
        // TODO: 필요 시 PlayerDataManager.Instance.SavePlayerData()와 연동하여 클라우드 저장
    }

    private void LoadLocalData()
    {
        if (PlayerPrefs.HasKey("UserQuestStatus"))
        {
            string json = PlayerPrefs.GetString("UserQuestStatus");
            currentStatus = JsonConvert.DeserializeObject<UserQuestStatus>(json);
        }
        else
        {
            currentStatus = new UserQuestStatus { lastAssignDate = "" };
        }
    }
    #endregion

    #region [UI Control]
    private void OpenPanel(bool isActive)
    {
        if (questPanel != null)
        {
            questPanel.gameObject.SetActive(isActive);
        }
    }
    #endregion
}

#region [Data Models & Enums]
public enum QuestType { PlayNum = 0, PlayTime = 1, TodayCheck = 2 }

[Serializable] public class QuestDefinitions { public List<DataQuest> daily_quests; }

[Serializable]
public class DataQuest 
{
    public string id;
    public string desc;
    public string type;
    public int goal;
    public int reward;
}

public struct userAttributes { }
public struct appAttributes { }
#endregion
}
