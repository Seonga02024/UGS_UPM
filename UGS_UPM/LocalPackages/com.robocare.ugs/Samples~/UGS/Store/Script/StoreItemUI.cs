namespace RoboCare.UGS
{
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RoboCare.UGS;


public class StoreItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button iconButton;
    [SerializeField] private GameObject descriptionPanel;
    [SerializeField] private GameObject soldOutOverlay; // 보유 중일 때 표시할 UI
    private StoreItem itemData;
    private int price = 0;

    private void Start()
    {
        buyButton.onClick.AddListener(OnBuyClicked);
        iconButton.onClick.AddListener(() =>
        {
            if (descriptionPanel.activeSelf)
            {
                descriptionPanel.SetActive(false);
            }
            else
            {
                descriptionPanel.SetActive(true);
            }
        });
    }

    public void Setup(StoreItem data, bool isOwned, int amount)
    {
        itemData = data;
        itemNameText.text = data.itemName;
        descriptionText.text = data.description;
        descriptionPanel.SetActive(false);
        LogApi.Log("[StoreItemUI] itemName : " + data.itemName + " / description : " + data.description);

        // 수량 표시 (예: "보유: 5개" 혹은 "구매 완료")
        if (isOwned)
        {
            buyButton.interactable = false; // 버튼 비활성화
            if (soldOutOverlay != null) soldOutOverlay.SetActive(true);
        }
        else
        {
            priceText.text = $"{data.price_usd}";
            price = (int)data.price_usd;
            buyButton.interactable = true;
            if (soldOutOverlay != null) soldOutOverlay.SetActive(false);
        }
    }

    public void UpdateOwnedState(bool isOwned)
    {
        // 소모품이 아닌 '영구 보유 아이템'의 경우 버튼을 비활성화하거나 덮개 표시
        if (soldOutOverlay != null) soldOutOverlay.SetActive(isOwned);
        buyButton.interactable = !isOwned;
    }

    private void OnBuyClicked()
    {
        if (PlayerDataManager.Gold >= price)
        {
            PlayerDataManager.Instance.BuyItem(itemData.id);

            // StoreManager를 통해 실제 구매 로직 실행
            StoreManager.Instance.GainItem(itemData.id, 1);
            
            // 구매 성공 후 UI 즉시 갱신 (수동 혹은 이벤트 방식)
            UpdateOwnedState(StoreManager.Instance.IsItemOwned(itemData.id));
            LogApi.Log($"{itemData.itemName} 구매 시도 완료!");   
        }
    }
}
}
