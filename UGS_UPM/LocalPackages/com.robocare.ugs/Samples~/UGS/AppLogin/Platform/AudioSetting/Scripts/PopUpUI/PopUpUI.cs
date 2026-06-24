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
            UIAnimation.PopupShow(popupBase, popupDuration, ignoreTimeScale: true);
        }

        public virtual void HidePopupUI()
        {
            UIAnimation.PopupHide(popupBase, popupDuration, () =>
            {
                // GameManager.Resource.Destroy(gameObject);
                //GameManager.UI.ClosePopUpUI();
            }, ignoreTimeScale: true);
        }
    }
}
