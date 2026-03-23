using System.Collections;
using TMPro;
using UnityEngine;

namespace RoboCare.UGS
{
    // Canvas LoadingLoginAndGetDataUI Prefab 에 붙이기 
public class LoadingLoginAndGetDataUI : MonoBehaviour
    {
        [SerializeField] private PlayerDataManager playerDataManager;

        #region [UI Components]
        [Header("UI Components")]
        [SerializeField] private GameObject blockPanel;
        [SerializeField] private TMP_Text blockPanelText;
        private string baseMessage = "데이터 불러오는 중";
        #endregion

        private void Start()
        {
            blockPanel.SetActive(true);
            blockPanelText.text = baseMessage;
            StartCoroutine(BlockPanelAnimateText());
            if (playerDataManager != null)
            {
                playerDataManager.GetDataCompleted += HandleGetDataCompleted;
            }
        }

        private void HandleGetDataCompleted()
        {
            blockPanel.SetActive(false);
            StopCoroutine(BlockPanelAnimateText());
        }

        IEnumerator BlockPanelAnimateText()
        {
            while (true)
            {
                blockPanelText.text = baseMessage + ".";
                yield return new WaitForSeconds(0.5f);

                blockPanelText.text = baseMessage + "..";
                yield return new WaitForSeconds(0.5f);

                blockPanelText.text = baseMessage + "...";
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
