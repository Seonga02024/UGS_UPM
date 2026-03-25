namespace RoboCare.UGS
{
using System.Collections.Generic;
using UnityEngine;

public class StorePanel : MonoBehaviour
{
    [Header("Layout Settings")]
    [SerializeField] private Transform contentParent; // ScrollView의 Content
    [SerializeField] private GameObject itemPrefab;    // StoreItemUI 프리팹

    private List<StoreItemUI> spawnedItems = new List<StoreItemUI>();

    /// <summary>
    /// StoreManager에서 Remote Config 데이터를 받아오면 호출됨
    /// </summary>
    public void InitializeStore(StoreData storeData)
    {
        // 1. 기존 슬롯들 청소
        foreach (var item in spawnedItems) Destroy(item.gameObject);
        spawnedItems.Clear();

        // 2. 새로운 슬롯 생성 시 상태 주입
        foreach (var item in storeData.items)
        {
            GameObject go = Instantiate(itemPrefab, contentParent);
            StoreItemUI itemUI = go.GetComponent<StoreItemUI>();

            // 생성할 때 보유 여부를 확인해서 바로 넘겨줌
            bool isOwned = StoreManager.Instance.IsItemOwned(item.id);
            int currentAmount = StoreManager.Instance.GetItemAmount(item.id);

            itemUI.Setup(item, isOwned, currentAmount);
            spawnedItems.Add(itemUI);
        }
    }

    // 상점을 닫았다가 다시 열 때 상태 갱신이 필요할 수 있음
    private void OnEnable()
    {
        RefreshAllItems();
    }

    public void RefreshAllItems()
    {
        foreach (var itemUI in spawnedItems)
        {
            // 여기서 각 슬롯의 상태만 다시 체크 (수량 등)
            // itemUI.UpdateOwnedState(...);
        }
    }
}
}
