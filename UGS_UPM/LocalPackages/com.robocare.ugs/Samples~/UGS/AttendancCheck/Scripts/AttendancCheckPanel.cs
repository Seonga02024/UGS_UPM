using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoboCare.UGS
{
    public class AttendancCheckPanel : MonoBehaviour
    {
        #region [Fields - UI & References]
        [Header("UI Components")]
        [SerializeField] private List<DayRewardUIItem> _instantiatedDayRewardItems;
        #endregion

        /// <summary>
        /// AttendancCheckManager 에서 받은 데이터를 적용하여 UI 로 표시 
        /// </summary>
        public void UpdateRewardLog(DayRewardDefinitions dayRewardDatas)
        {
            int number = 0;
            // 활성화된 모든 퀘스트에 대해 UI 아이템을 생성합니다.
            foreach (DayRewardData dayReward in dayRewardDatas.day_rewards)
            {
                _instantiatedDayRewardItems[number].SetReward(dayReward);
            }
        }
    }
}
