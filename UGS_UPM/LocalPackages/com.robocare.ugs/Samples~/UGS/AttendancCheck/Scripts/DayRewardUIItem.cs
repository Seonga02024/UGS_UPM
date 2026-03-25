using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    public class DayRewardUIItem : MonoBehaviour
    {
        #region [Fields - UI & References]
        [Header("UI References")]
        [SerializeField] private GameObject completedBlock;
        [SerializeField] private TextMeshProUGUI idText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Button getRewardBtn;
        private DayRewardData _data = null;
        private bool isCompleted = false;
        #endregion

        #region [Initialization & Setting]
        private void Start()
        {
            getRewardBtn.onClick.AddListener(CheckReward);
            completedBlock.SetActive(false);
        }

        private void CheckReward()
        {
            if (isCompleted) return;
            isCompleted = true;
            PlayerDataManager.Instance.SendDayReward(_data.id);
            completedBlock.SetActive(true);
        }
        #endregion

        #region [Public Method]
        public void SetReward(DayRewardData rewardData)
        {
            _data = rewardData;
            idText.text = _data.id.ToString();
            rewardText.text = _data.reward.ToString();

            // 특정 조건에 따른 아이템 이미 습득 했는지 아닌지 표시해주는 곳 
            // if (PlayerDataManager.Instance.CurrentPlayerData)
            // {
            //     completedBlock.SetActive(true);
            //     isCompleted = true;
            // }
            // else
            // {
            //     completedBlock.SetActive(false);
            //     isCompleted = false;
            //     getRewardBtn.enabled = false;
            // }
        }
        #endregion
    }
}
