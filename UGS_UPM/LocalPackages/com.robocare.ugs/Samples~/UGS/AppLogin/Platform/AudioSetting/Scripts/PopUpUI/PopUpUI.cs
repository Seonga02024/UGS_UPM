using UnityEngine;

namespace RoboCare.UGS
{
    public class PopUpUI : BaseUI
    {
        public GameObject popupBase;
        public float popupDuration = 0.5f;

        protected override void Awake()
        {
            base.Awake();
        }

        public virtual void ShowPopupUI()
        {
            DOTweenAnimation.PopupShow(popupBase, popupDuration, ignoreTimeScale: true);
        }

        public virtual void HidePopupUI()
        {
            DOTweenAnimation.PopupHide(popupBase, popupDuration, () =>
            {
                // GameManager.Resource.Destroy(gameObject);
                //GameManager.UI.ClosePopUpUI();
            }, ignoreTimeScale: true);
        }
    }
}
