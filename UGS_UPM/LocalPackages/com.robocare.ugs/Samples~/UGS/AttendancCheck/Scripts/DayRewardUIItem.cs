using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    public class DayRewardUIItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject completedBlock;
        [SerializeField] private TextMeshProUGUI idText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private Button getRewardBtn;

        private DayRewardData _data;

        private void Start()
        {
            if (getRewardBtn != null)
            {
                getRewardBtn.onClick.AddListener(CheckReward);
            }

            SetClaimState(false, false);
        }

        private void CheckReward()
        {
            if (_data == null)
            {
                return;
            }

            if (getRewardBtn == null || !getRewardBtn.interactable)
            {
                return;
            }

            AttendancCheckManager.Instance?.TryClaimAttendance(_data.id);
        }

        public void SetReward(DayRewardData rewardData)
        {
            _data = rewardData;
            if (idText != null)
            {
                idText.text = _data.id;
            }

            if (rewardText != null)
            {
                rewardText.text = _data.reward.ToString();
            }
        }

        public void SetClaimState(bool isCompleted, bool canClaim)
        {
            if (completedBlock != null)
            {
                completedBlock.SetActive(isCompleted);
            }

            if (getRewardBtn != null)
            {
                getRewardBtn.interactable = canClaim;
            }
        }
    }
}
