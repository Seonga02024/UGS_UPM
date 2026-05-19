using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RoboCare.UGS
{
    /*
     * 사용 방법:
     * 1) netCheckPanel(네트워크 경고 UI)과 quitBtn을 인스펙터에 연결합니다.
     * 2) Awake에서 인터넷 연결 체크를 즉시 수행하고 결과에 따라 패널 표시를 제어합니다.
     * 3) 필요 시 주기 체크 코루틴을 추가해 실시간 상태 감시로 확장할 수 있습니다.
     */
    public class NetCheckManager : MonoBehaviour
    {
        [SerializeField] private GameObject backPanel;
        [SerializeField] private GameObject netCheckPanel;
        [SerializeField] private Button quitBtn;
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text buttonText;

        public bool IsInternetAvailable { get; private set; }
        private const string CheckUrl = "https://clients3.google.com/generate_204";
        private const int TimeoutSec = 3;
        [SerializeField] private LoginSuccessPanel loginSuccessPanel;

        private void OnEnable()
        {
            backPanel.SetActive(false);
            netCheckPanel.SetActive(false);
            if (loginSuccessPanel != null)
            {
                loginSuccessPanel.FinishAppLogin += HandleLoginSuccessPanelFinished;
            }
        }

        private void OnDisable()
        {
            loginSuccessPanel.FinishAppLogin -= HandleLoginSuccessPanelFinished;
        }

        private void HandleLoginSuccessPanelFinished()
        {
            backPanel.SetActive(true);
            netCheckPanel.SetActive(false);
            StartCoroutine(CheckInternetNow());

            quitBtn.onClick.RemoveAllListeners();
#if UNITY_IOS
            // Apple HIG: iOS 앱은 스스로 종료하지 않는다 → 재시도 버튼으로 동작
            mainText.text = "인터넷 연결이 필요합니다";
            detailText.text = "Wi-Fi 또는 셀룰러 데이터 연결을 \n 확인한 후 다시 시도해 주세요.";
            buttonText.text = "다시 시도";
            quitBtn.onClick.AddListener(RetryInternetCheck);
#else
            mainText.text = "인터넷 연결 상태를 \n 확인해 주세요.";
            detailText.text = "인터넷이 연결되지 않아 \n 게임을 실행할 수 없습니다.";
            buttonText.text = "종료하기";
            quitBtn.onClick.AddListener(() => Application.Quit());
#endif
        }

#if UNITY_IOS
        private void RetryInternetCheck()
        {
            StartCoroutine(CheckInternetNow());
        }
#endif

        private IEnumerator CheckInternetNow()
        {
            if (LoginTokenReader.Instance.currentPlatform == PlatformType.BOMI1 || LoginTokenReader.Instance.currentPlatform == PlatformType.BOMI2)
            {
                backPanel.SetActive(false);
                netCheckPanel.SetActive(false);
                yield break;
            }

            // 1) 로컬 네트워크 연결 유무
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                IsInternetAvailable = false;
                backPanel.SetActive(true);
                netCheckPanel.SetActive(true);
                LogApi.Log("No network interface reachable.");
                yield break;
            }

            // 2) 실제 외부 접속 확인
            using var req = UnityWebRequest.Get(CheckUrl);
            req.timeout = TimeoutSec;
            yield return req.SendWebRequest();

            IsInternetAvailable =
                req.result == UnityWebRequest.Result.Success &&
                req.responseCode == 204;

            netCheckPanel.SetActive(IsInternetAvailable ? false : true);
            backPanel.SetActive(IsInternetAvailable ? false : true);

            LogApi.Log($"Internet available: {IsInternetAvailable}");
        }
    }
}
