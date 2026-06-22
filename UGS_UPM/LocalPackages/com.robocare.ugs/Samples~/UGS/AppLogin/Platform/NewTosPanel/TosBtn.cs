using UnityEngine;
using UnityEngine.UI;

public class TosBtn : MonoBehaviour
{
    public Button tosPanel;
    [SerializeField] private SimpleTermsPanelController simpleTermsPanelController;

    void Start()
    {
        tosPanel.onClick.AddListener(ShowTosPanel);
    }

    private void ShowTosPanel()
    {
        if (simpleTermsPanelController == null)
        {
            Debug.LogWarning("SimpleTermsPanelController not found. Attempting to find in scene...");
            simpleTermsPanelController = FindAnyObjectByType<SimpleTermsPanelController>();
        }
        if (simpleTermsPanelController)
        {
            Debug.Log("Showing Terms of Service panel.");
            simpleTermsPanelController.SettingJustShowPanel();
            simpleTermsPanelController.gameObject.SetActive(true);
        }
    }
}
