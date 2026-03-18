using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }
    [SerializeField] private LoginManager loginManager;

    #region [Settings & Keys]
    private const string CLOUD_DATA_KEY = "player_data";

    // JSON 변환 시 리스트 중복 방지를 위한 설정
    private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };
    #endregion

    #region [Player Data]
    /// <summary> 공통 게임 재화 (Gold) </summary>
    public static int Gold;

    /// <summary> 현재 세션에서 사용 중인 플레이어 데이터 </summary>
    [Header("Current Data Status")]
    [SerializeField] public static PlayerData CurrentPlayerData;

    /// <summary> 데이터가 성공적으로 로드되었는지 확인 </summary>
    private bool isLoadPlayerData = false;
    #endregion
    public event Action GetDataCompleted;

    private void Start()
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

        // 초기화 시 데이터 비우기
        CurrentPlayerData = null;
        if (loginManager != null)
        {
            loginManager.LoginCompleted += HandleLoginCompleted;
        }
    }

    private void HandleLoginCompleted()
    {
        _ = PostLoginSequence();
    }

    private void OnApplicationQuit() => SaveProcess();

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveProcess();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveProcess();
    }

    /// <summary> 로그인 성공 후 데이터 로드 시퀀스 </summary>
    private async Task PostLoginSequence()
    {
        await UpdateCoin();   // 서버에서 코인 정보 수신
        await LoadPlayerData(); // 클라우드 데이터 수신
        CurrentPlayerData.name = await AuthenticationService.Instance.GetPlayerNameAsync();
    }

    #region [Cloud Save Service]
    /// <summary> 서버로부터 데이터를 불러오고 로컬과 동기화 </summary>
    private async Task LoadPlayerData()
    {
        try
        {
            CurrentPlayerData = null;
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CLOUD_DATA_KEY });

            if (data.TryGetValue(CLOUD_DATA_KEY, out var item))
            {
                // JSON -> 객체 변환
                CurrentPlayerData = JsonConvert.DeserializeObject<PlayerData>(item.Value.GetAsString(), jsonSettings);
            }
            else
            {
                Debug.Log("서버 데이터가 없습니다. 초기 데이터 생성.");
                CurrentPlayerData = new PlayerData();
                CurrentPlayerData.InitPlayerData();
                await SavePlayerData(); // 초기 상태 서버에 저장
            }

            SettingPlayerPrefs(); // 로컬 캐시 갱신
            isLoadPlayerData = true;
            GetDataCompleted?.Invoke(); // 데이터 로드 완료 
        }
        catch (Exception e)
        {
            Debug.LogError($"[Cloud Load Error] {e.Message}");
        }
    }

    /// <summary> 현재 메모리의 데이터를 서버에 업로드 </summary>
    private async Task SavePlayerData()
    {
        if (CurrentPlayerData == null) return;
        try
        {
            var dataToSave = new Dictionary<string, object> { { CLOUD_DATA_KEY, CurrentPlayerData } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(dataToSave);
            Debug.LogError("서버 저장 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Cloud Save Error] {e.Message}");
        }
    }
    #endregion

    #region [Economy & Analytics]
    private async Task UpdateCoin()
    {
        var parameters = new Dictionary<string, object> {
            { "playerId", AuthenticationService.Instance.PlayerId },
            { "action", "GET_PLAYER_GOLD_DATA" }
        };
        var response = await CloudCodeService.Instance.CallEndpointAsync<GoldResponse>("GetPlayerData", parameters);
        Gold = response.success ? response.gold : 0;
        Debug.Log($"통합 재화 데이터 로드 성공 (Gold: {Gold})");
    }

    public async void SaveProcess()
    {
        if (!isLoadPlayerData) return;

        Debug.Log("최종 저장 프로세스 시작...");
        LoadFromPlayerPrefs(); // PlayerPrefs -> Memory 동기화
        await SavePlayerData();  // Memory -> Cloud 업로드
    }
    #endregion

    #region [플레이어 계정/이름 설정]
    public async Task DeletePlayer()
    {
        try
        {
            await AuthenticationService.Instance.DeleteAccountAsync();
            Debug.Log("계정 삭제 완료");
        }
        catch (Exception e) { Debug.LogError(e.Message); }
    }

    public async Task SavePlayerName(string playerName)
    {
        try
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
            CurrentPlayerData.name = await AuthenticationService.Instance.GetPlayerNameAsync();
        }
        catch (Exception e) { Debug.LogError(e.Message); }
    }
    #endregion

    #region [Public Method]
    /// <summary>
    /// 서버에 획득한 골드를 전송하고 최신 골드 수치를 업데이트함
    /// </summary>
    /// <param name="count">저장할 골드 수량</param>
    public async void SendCoin(int count)
    {
        Debug.Log($"[CloudCode] 골드 저장 요청: {count}");
        try
        {
            var parameters = new Dictionary<string, object> {
                { "playerId", AuthenticationService.Instance.PlayerId },
                { "action", "SAVE_GOLD" },
                { "gold", count }
            };

            // Cloud Code의 "GetPlayerData" 엔드포인트 호출
            var response = await CloudCodeService.Instance.CallEndpointAsync<GoldResponse>("GetPlayerData", parameters);

            if (response.success)
            {
                Gold = response.gold;
                Debug.Log($"[CloudCode] 골드 저장 완료. 현재 잔액: {Gold}");
            }
            else
            {
                Debug.LogError($"[CloudCode] 저장 실패: {response.message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudCode Error] SendCoin 중 오류 발생: {e.Message}");
        }
    }

    public bool CheckCompleteGameRewards(string id)
    {
        return CurrentPlayerData.completeGameRewards.Contains(id);
    }
    #endregion
    

    #region [PlayerPrefs (Local)] 게임 별 커스텀 필요 
    /// <summary> 메모리 데이터를 PlayerPrefs로 복사 </summary>
    public void SettingPlayerPrefs()
    {
        if (CurrentPlayerData == null) return;

        PlayerPrefs.SetInt("Lifes", CurrentPlayerData.lifes);
        PlayerPrefs.SetInt("Music", CurrentPlayerData.Music);
        PlayerPrefs.SetInt("Sound", CurrentPlayerData.Sound);
        PlayerPrefs.SetInt("Lauched", CurrentPlayerData.Lauched);
        PlayerPrefs.SetInt("Gems", Gold);
        PlayerPrefs.SetFloat("RestLifeTimer", CurrentPlayerData.RestLifeTimer);
        PlayerPrefs.SetString("DateOfExit", CurrentPlayerData.DateOfExit);
        PlayerPrefs.SetInt("OpenLevel", CurrentPlayerData.OpenLevel);
        PlayerPrefs.SetInt("Rated", CurrentPlayerData.Rated);

        // 복합 데이터(퀘스트, 리워드) JSON 저장
        PlayerPrefs.SetString("UserQuestStatus", JsonConvert.SerializeObject(CurrentPlayerData.questStatus));
        PlayerPrefs.SetString("CompleteGameRewards", JsonConvert.SerializeObject(CurrentPlayerData.completeGameRewards));

        foreach (var item in CurrentPlayerData.Items)
        {
            PlayerPrefs.SetInt(item.id.ToString(), item.count);
        }

        PlayerPrefs.Save();
    }

    /// <summary> PlayerPrefs 데이터를 메모리로 로드 </summary>
    private void LoadFromPlayerPrefs()
    {
        if (CurrentPlayerData == null) CurrentPlayerData = new PlayerData();

        CurrentPlayerData.lifes = PlayerPrefs.GetInt("Lifes", 5);
        CurrentPlayerData.Music = PlayerPrefs.GetInt("Music", 1);
        CurrentPlayerData.Sound = PlayerPrefs.GetInt("Sound", 1);
        CurrentPlayerData.Lauched = PlayerPrefs.GetInt("Lauched", 0);
        CurrentPlayerData.RestLifeTimer = PlayerPrefs.GetFloat("RestLifeTimer", 0f);
        CurrentPlayerData.DateOfExit = PlayerPrefs.GetString("DateOfExit", "");
        CurrentPlayerData.OpenLevel = PlayerPrefs.GetInt("OpenLevel", 1);
        CurrentPlayerData.Rated = PlayerPrefs.GetInt("Rated", 0);

        // JSON 데이터 복원
        if (PlayerPrefs.HasKey("UserQuestStatus"))
            CurrentPlayerData.questStatus = JsonConvert.DeserializeObject<UserQuestStatus>(PlayerPrefs.GetString("UserQuestStatus"), jsonSettings);

        if (PlayerPrefs.HasKey("CompleteGameRewards"))
            CurrentPlayerData.completeGameRewards = JsonConvert.DeserializeObject<List<string>>(PlayerPrefs.GetString("CompleteGameRewards"), jsonSettings);

        if (CurrentPlayerData.completeGameRewards == null)
            CurrentPlayerData.completeGameRewards = new List<string>();

        foreach (var item in CurrentPlayerData.Items)
        {
            item.count = PlayerPrefs.GetInt(item.id.ToString(), 0);
        }

        Debug.Log("로컬 데이터를 메모리에 동기화 완료");
    }
    #endregion
}