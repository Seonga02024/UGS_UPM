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
        public bool isCheckPlayerData = true;
        private string baseMessage = "데이터 불러오는 중";
        private static bool alreadyUpdate = false;
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
            if(isCheckPlayerData == false){
                if (LoginManager.Instance != null)
                {
                    LoginManager.Instance.LoginCompleted += HandleGetDataCompleted;
                }
            }

            if(alreadyUpdate){
                HandleGetDataCompleted();
            }
        }

        private void OnDestroy()
        {
            if(playerDataManager) playerDataManager.GetDataCompleted -= HandleGetDataCompleted;
            if(LoginManager.Instance) LoginManager.Instance.LoginCompleted -= HandleGetDataCompleted;
        }

        private void HandleGetDataCompleted()
        {
            blockPanel.SetActive(false);
            StopCoroutine(BlockPanelAnimateText());
            alreadyUpdate = true;
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
