using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace RoboCare.UGS
{
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
                Debug.Log("No network interface reachable.");
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

            Debug.Log($"Internet available: {IsInternetAvailable}");
        }
    }
}
