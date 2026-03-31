using System.Collections;
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
        [SerializeField] private GameObject netCheckPanel;
        [SerializeField] private Button quitBtn;

        public bool IsInternetAvailable { get; private set; }
        private const string CheckUrl = "https://clients3.google.com/generate_204";
        private const int TimeoutSec = 3;

        private void Awake()
        {
            StartCoroutine(CheckInternetNow());
            quitBtn.onClick.AddListener(() => Application.Quit());
        }

        private IEnumerator CheckInternetNow()
        {
            // 1) 로컬 네트워크 연결 유무
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                IsInternetAvailable = false;
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

            LogApi.Log($"Internet available: {IsInternetAvailable}");
        }
    }
}
