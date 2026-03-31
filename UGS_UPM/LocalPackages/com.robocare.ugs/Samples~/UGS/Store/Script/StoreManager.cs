namespace RoboCare.UGS
{
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

/*
 * 사용 방법:
 * 1) Remote Config에 Store_Items(json) 키를 생성하고 스토어 아이템 목록을 등록합니다.
        {
            "items": [{
                "id": "gold_pack_01",
                "itemName": "초보자 골드 팩",
                "description": "50,000 골드 + 보너스 5,000 골드",
                "price_usd": 5,
                "icon_name": "icon_gold_small"
            }, {
                "id": "gem_special_02",
                "itemName": "장인의 보석 상자",
                "description": "보석 1,000개",
                "price_usd": 20,
                "icon_name": "icon_gem_chest"
            }]
        }
 * 2) LoginManager.LoginCompleted 이벤트 수신 후 InitializeSequence로 Cloud Save/Remote Config를 동기화합니다.
 * 3) Cloud Save 키 INVENTORY_KEY(PlayerInventory)로 로컬 인벤토리를 서버와 동기화합니다.
 */
public class StoreManager : MonoBehaviour
{
    public static StoreManager Instance { get; private set; }

    #region [Fields - UI & References]
    [Header("UI Components")]
    [SerializeField] private StorePanel storePanel;
    [SerializeField] private Button storeBtn;
    [SerializeField] private Button closeBtn;
    #endregion

    private Dictionary<string, int> inventory = new Dictionary<string, int>();
    private const string INVENTORY_KEY = "PlayerInventory";

    #region [Unity Lifecycle]
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            if (LoginManager.Instance != null)
            {
                LoginManager.Instance.LoginCompleted += HandleLoginCompleted;
                if (LoginManager.Instance.IsLoggedIn)
                {
                    HandleLoginCompleted();
                }
            }
        }
        else
        {
            HandleLoginCompleted();
        }
    
    }
    
    private void HandleLoginCompleted()
    {
        _ = PostLoginSequence();
    }

    private async Task PostLoginSequence()
    {
        // 1. 서비스 초기화 (Cloud Save 사용을 위해 필수)
        await InitializeUnityServices();

        // 2. 클라우드에서 데이터를 가져와서 로컬 PlayerPrefs 동기화
        await SyncCloudToLocal();

        // 3. 로컬 데이터 로드하여 메모리(Dictionary) 구성
        LoadPlayerItemsData();

        // 4. Remote Config를 통해 스토어 정보 받아오기
        await CheckAndAssignStoreItems();

        // 5. 버튼 이벤트 바인딩
        storeBtn.onClick.AddListener(() => OpenPanel(true));
        closeBtn.onClick.AddListener(() => OpenPanel(false));
    }
    
    private void OnApplicationFocus(bool focus)
        {
            if (!focus) // 포커스를 잃을 때(백그라운드 이동 시) 저장
            {
                SyncLocalToCloud();
            }
        }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SyncLocalToCloud();
    }

    // 앱 종료 시 저장
    private void OnApplicationQuit()
    {
        SyncLocalToCloud();
    }
    #endregion

    #region [Cloud Save Sync]
    /// <summary>
    /// 클라우드 데이터를 가져와 로컬 PlayerPrefs에 덮어씌웁니다.
    /// </summary>
    private async Task SyncCloudToLocal()
    {
        try
        {
            // Cloud Save에서 INVENTORY_KEY 값 하나를 가져옴
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { INVENTORY_KEY });

            if (data.TryGetValue(INVENTORY_KEY, out var item))
            {
                string cloudJson = item.Value.GetAsString();
                if (!string.IsNullOrEmpty(cloudJson))
                {
                    PlayerPrefs.SetString(INVENTORY_KEY, cloudJson);
                    PlayerPrefs.Save();
                    LogApi.Log("[Store] Cloud Data를 로컬로 동기화 완료");
                }
            }
        }
        catch (Exception e)
        {
            LogApi.LogError($"[Store] Cloud Load 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 로컬 PlayerPrefs에 있는 JSON 데이터를 클라우드에 업로드합니다.
    /// </summary>
    private async void SyncLocalToCloud()
    {
        string localJson = PlayerPrefs.GetString(INVENTORY_KEY, "");
        if (string.IsNullOrEmpty(localJson)) return;

        try
        {
            var data = new Dictionary<string, object> { { INVENTORY_KEY, localJson } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            LogApi.Log("[Store] 로컬 데이터를 Cloud로 업로드 완료");
        }
        catch (Exception e)
        {
            LogApi.LogError($"[Store] Cloud Save 실패: {e.Message}");
        }
    }
    #endregion

    #region [Initialization & Remote Config]
    private async Task InitializeUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        LogApi.Log("[Store] Unity Services Initialized");
    }
    /// <summary>
    /// Remote Config를 통해 스토어 정보를 받아오고 할당 
    /// </summary>
    private async Task CheckAndAssignStoreItems()
    {
        // 인자값으로 빈 구조체 전달하여 최신 Config Fetch
        await RemoteConfigService.Instance.FetchConfigsAsync(new userAttributes(), new appAttributes());
        
        string json = RemoteConfigService.Instance.appConfig.GetJson("Store_Items");
        if (string.IsNullOrEmpty(json)) return;

        StoreData storeData = JsonConvert.DeserializeObject<StoreData>(json);

        // UI 패널에 데이터 전달하여 생성 요청
        if (storePanel != null)
        {
            LogApi.Log("[StoreManager] storeData : " + storeData.items.Count);
            storePanel.InitializeStore(storeData);
        }
    }

    #endregion

    #region [Store Management]
    /// <summary>
    /// 아이템을 획득했을 때 호출하는 함수
    /// </summary>
    /// <param name="itemId">아이템 고유 ID</param>
    /// <param name="amount">획득한 수량 (기본값 1)</param>
    public void GainItem(string itemId, int amount = 1)
    {
        if (amount <= 0) return;

        // 1. 메모리 데이터(Dictionary) 업데이트
        if (inventory.ContainsKey(itemId))
        {
            inventory[itemId] += amount;
        }
        else
        {
            inventory[itemId] = amount;
        }

        // 2. 변경된 데이터를 PlayerPrefs에 즉시 저장
        SaveInventoryToDisk();

        // 3. (옵션) 획득 연출이나 UI 업데이트 호출
        LogApi.Log($"<color=yellow>[아이템 획득]</color> {itemId} x{amount} (총 보유: {inventory[itemId]})");

        // UI가 열려있다면 즉시 갱신하도록 이벤트를 발생시키거나 직접 호출하세요.
        // UIManager.Instance.UpdateInventoryUI(); 
    }

    // 해당 아이템을 가지고 있는지 확인
    public bool IsItemOwned(string itemId)
    {
        return inventory.ContainsKey(itemId) && inventory[itemId] > 0;
    }

    // 아이템의 현재 수량 가져오기
    public int GetItemAmount(string itemId)
    {
        if (inventory.TryGetValue(itemId, out int amount))
        {
            return amount;
        }
        return 0; // 없으면 0개
    }

    #endregion

    #region [Data Persistence]

    private void SaveInventoryToDisk()
    {
        InventoryData data = new InventoryData();
        foreach (var pair in inventory)
        {
            data.items.Add(new ItemEntry(pair.Key, pair.Value));
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(INVENTORY_KEY, json);
        PlayerPrefs.Save();
        
        // 실시간으로 중요 데이터라면 여기서 바로 클라우드 저장을 호출할 수도 있습니다.
        // SyncLocalToCloud(); 
    }

    private void LoadPlayerItemsData()
    {
        string json = PlayerPrefs.GetString(INVENTORY_KEY, "");
        inventory.Clear();

        if (!string.IsNullOrEmpty(json))
        {
            InventoryData data = JsonUtility.FromJson<InventoryData>(json);
            foreach (var entry in data.items)
            {
                inventory[entry.id] = entry.amount;
            }
        }
        LogApi.Log($"[Store] 인벤토리 로컬 로드 완료: {inventory.Count}종");
    }

    #endregion

    #region [UI Control]
    private void OpenPanel(bool isActive)
    {
        if (storePanel != null)
        {
            storePanel.gameObject.SetActive(isActive);
        }
    }
    #endregion
}

#region [Data Models & Enums]
[System.Serializable]
public class StoreItem
{
    public string id;
    public string itemName;
    public string description;
    public float price_usd;
    public string icon_name;
}

[System.Serializable]
public class StoreData
{
    public List<StoreItem> items;
}

[System.Serializable]
public class InventoryData
{
    public List<ItemEntry> items = new List<ItemEntry>();
}

[System.Serializable]
public class ItemEntry
{
    public string id;
    public int amount;

    public ItemEntry(string id, int amount)
    {
        this.id = id;
        this.amount = amount;
    }
}
#endregion

}
