using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEditor;
using UnityEngine;

namespace RoboCare.UGS
{
    public class ServerEventManager : MonoBehaviour
    {
        public static ServerEventManager Instance { get; private set; }
        
        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #region GetOrInitPlayerMoneyEndpoint 재화 정보 획득

        /*
        * 사용 방법:
        * 1) Remote congif free_money (long) / min_money (long) 
        */

        private const string GetOrInitPlayerMoneyEndpoint = "GetOrInitPlayerMoney";
        public event Action<GetOrInitPlayerMoneyResponse> OnGetOrInitPlayerMoneyCompleted;

        public async void GetOrInitPlayerMoney(
        GetOrInitPlayerMoneyRequest request = null,
        Action<GetOrInitPlayerMoneyResponse> callback = null)
        {
            GetOrInitPlayerMoneyResponse response = await GetOrInitPlayerMoneyAsync(request);
            callback?.Invoke(response);
        }

        /*
        var tcs = new TaskCompletionSource<bool>();
        var req = new GetOrInitPlayerMoneyRequest { PLAYER_ID = AuthenticationService.Instance.PlayerId };
        ServerEventManager.Instance.GetOrInitPlayerMoney(req, res =>
        {
            tcs.TrySetResult(res.success);
            if (res.success)
            {
                ApplyAuthoritativeMoney(res.money);
            }
            LogApi.Log($"[PlayerDataManager][GetOrInitPlayerMoneyRequest Callback] success={res.success}, money={res.money}, updated={res.updated}");
        });
        */

        public async Task<GetOrInitPlayerMoneyResponse> GetOrInitPlayerMoneyAsync(GetOrInitPlayerMoneyRequest request = null)
        {
            var parameters = new Dictionary<string, object>();
            if (request != null && !string.IsNullOrWhiteSpace(request.PLAYER_ID))
            {
                parameters["playerId"] = request.PLAYER_ID;
            }

            try
            {
                GetOrInitPlayerMoneyResponse response =
                    await CloudCodeService.Instance.CallEndpointAsync<GetOrInitPlayerMoneyResponse>(
                        GetOrInitPlayerMoneyEndpoint,
                        parameters);

                if (response == null)
                {
                    response = BuildMoneyErrorResponse("EMPTY_RESPONSE");
                }

                NotifyGetOrInitPlayerMoneyResult(response);
                return response;
            }
            catch (Exception e)
            {
                LogApi.LogError($"[ServerEventManager] GetOrInitPlayerMoney failed: {e.Message}");
                var errorResponse = BuildMoneyErrorResponse("CLOUD_CODE_ERROR");
                NotifyGetOrInitPlayerMoneyResult(errorResponse);
                return errorResponse;
            }
        }

        private void NotifyGetOrInitPlayerMoneyResult(GetOrInitPlayerMoneyResponse response)
        {
            OnGetOrInitPlayerMoneyCompleted?.Invoke(response);
        }

        private static GetOrInitPlayerMoneyResponse BuildMoneyErrorResponse(string message)
        {
            return new GetOrInitPlayerMoneyResponse
            {
                success = false,
                money = 0L,
                message = message
            };
        }
        #endregion
    }
}
