using System.Collections.Generic;
using UnityEngine;

namespace RoboCare.UGS
{
    public class AttendancCheckPanel : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private List<DayRewardUIItem> _instantiatedDayRewardItems;

        public void UpdateRewardLog(DayRewardDefinitions dayRewardDatas)
        {
            if (dayRewardDatas?.day_rewards == null || _instantiatedDayRewardItems == null)
            {
                return;
            }

            int count = Mathf.Min(dayRewardDatas.day_rewards.Count, _instantiatedDayRewardItems.Count);
            for (int i = 0; i < count; ++i)
            {
                _instantiatedDayRewardItems[i].SetReward(dayRewardDatas.day_rewards[i]);
                _instantiatedDayRewardItems[i].SetClaimState(false, false);
            }
        }

        public void ApplyClaimState(int claimCount, bool hasClaimedToday)
        {
            if (_instantiatedDayRewardItems == null)
            {
                return;
            }

            int nextIndex = claimCount + 1;

            for (int i = 0; i < _instantiatedDayRewardItems.Count; ++i)
            {
                int dayNumber = i + 1;
                bool isCompleted = dayNumber <= claimCount;
                bool canClaimNow = dayNumber == nextIndex && !hasClaimedToday;

                _instantiatedDayRewardItems[i].SetClaimState(isCompleted, canClaimNow);
            }
        }
    }
}
