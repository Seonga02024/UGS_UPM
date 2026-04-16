using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RoboCare.UGS;
using Unity.Services.CloudCode;
using UnityEngine;

public class ServerEventManager : SingletonBehaviour<ServerEventManager>
{
    private const string GetOrInitPlayerMoneyEndpoint = "GetOrInitPlayerMoney";
    private const string ClaimAttendanceRewardEndpoint = "ClaimAttendanceReward";
    private const string CompleteQuestRewardEndpoint = "CompleteQuestReward";
    private const string CompleteGameRewardEndpoint = "CompleteGameReward";
    public event Action<ValidateAndSetCurrentGameResponse> OnValidateAndSetCurrentGameCompleted;
    public event Action<GetOrInitPlayerMoneyResponse> OnGetOrInitPlayerMoneyCompleted;
    public event Action<ClaimAttendanceRewardResponse> OnClaimAttendanceRewardCompleted;
    public event Action<CompleteQuestRewardResponse> OnCompleteQuestRewardCompleted;
    public event Action<CompleteGameRewardResponse> OnCompleteGameRewardCompleted;

    private static bool IsValidRequest(ClaimAttendanceRewardRequest request)
    {
        return request != null;
    }

    private static bool IsValidRequest(CompleteQuestRewardRequest request)
    {
        return request != null && !string.IsNullOrWhiteSpace(request.QUEST_ID);
    }

    private static bool IsValidRequest(CompleteGameRewardRequest request)
    {
        return request != null && !string.IsNullOrWhiteSpace(request.REWARD_ID);
    }

    private static ClaimAttendanceRewardResponse BuildAttendanceErrorResponse(string errorCode)
    {
        return new ClaimAttendanceRewardResponse
        {
            success = false,
            errorCode = errorCode,
            currentMoney = 0,
            rewardDay = string.Empty,
            reward = 0,
            claimCount = 0,
            monthKey = string.Empty
        };
    }

    private static CompleteQuestRewardResponse BuildCompleteQuestErrorResponse(string errorCode)
    {
        return new CompleteQuestRewardResponse
        {
            success = false,
            errorCode = errorCode,
            questId = string.Empty,
            reward = 0,
            currentMoney = 0L,
            updated = false,
            message = errorCode
        };
    }

    private static CompleteGameRewardResponse BuildCompleteGameRewardErrorResponse(string errorCode)
    {
        return new CompleteGameRewardResponse
        {
            success = false,
            errorCode = errorCode,
            rewardId = string.Empty,
            reward = 0,
            currentMoney = 0L,
            updated = false,
            message = errorCode
        };
    }

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
            Debug.LogError($"[ServerEventManager] GetOrInitPlayerMoney failed: {e.Message}");
            var errorResponse = BuildMoneyErrorResponse("CLOUD_CODE_ERROR");
            NotifyGetOrInitPlayerMoneyResult(errorResponse);
            return errorResponse;
        }
    }

    public async void GetOrInitPlayerMoney(
        GetOrInitPlayerMoneyRequest request = null,
        Action<GetOrInitPlayerMoneyResponse> callback = null)
    {
        GetOrInitPlayerMoneyResponse response = await GetOrInitPlayerMoneyAsync(request);
        callback?.Invoke(response);
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

    public async Task<ClaimAttendanceRewardResponse> ClaimAttendanceRewardAsync(
        ClaimAttendanceRewardRequest request)
    {
        if (request == null)
        {
            request = new ClaimAttendanceRewardRequest();
        }

        if (!IsValidRequest(request))
        {
            var invalidResponse = BuildAttendanceErrorResponse("INVALID_PARAMS");
            NotifyClaimAttendanceRewardResult(invalidResponse);
            return invalidResponse;
        }

        var parameters = new Dictionary<string, object>();

        try
        {
            ClaimAttendanceRewardResponse response =
                await CloudCodeService.Instance.CallEndpointAsync<ClaimAttendanceRewardResponse>(
                    ClaimAttendanceRewardEndpoint,
                    parameters);

            if (response == null)
            {
                response = BuildAttendanceErrorResponse("EMPTY_RESPONSE");
            }

            NotifyClaimAttendanceRewardResult(response);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerEventManager] ClaimAttendanceReward failed: {e.Message}");
            var errorResponse = BuildAttendanceErrorResponse("CLOUD_CODE_ERROR");
            NotifyClaimAttendanceRewardResult(errorResponse);
            return errorResponse;
        }
    }

    public async void ClaimAttendanceReward(
        ClaimAttendanceRewardRequest request = null,
        Action<ClaimAttendanceRewardResponse> callback = null)
    {
        ClaimAttendanceRewardResponse response = await ClaimAttendanceRewardAsync(request);
        callback?.Invoke(response);
    }

    private void NotifyClaimAttendanceRewardResult(ClaimAttendanceRewardResponse response)
    {
        OnClaimAttendanceRewardCompleted?.Invoke(response);
    }

    public async Task<CompleteQuestRewardResponse> CompleteQuestRewardAsync(CompleteQuestRewardRequest request)
    {
        if (!IsValidRequest(request))
        {
            var invalidResponse = BuildCompleteQuestErrorResponse("INVALID_PARAMS");
            NotifyCompleteQuestRewardResult(invalidResponse);
            return invalidResponse;
        }

        var parameters = new Dictionary<string, object>
        {
            { "questId", request.QUEST_ID }
        };

        try
        {
            CompleteQuestRewardResponse response =
                await CloudCodeService.Instance.CallEndpointAsync<CompleteQuestRewardResponse>(
                    CompleteQuestRewardEndpoint,
                    parameters);

            if (response == null)
            {
                response = BuildCompleteQuestErrorResponse("EMPTY_RESPONSE");
            }

            NotifyCompleteQuestRewardResult(response);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerEventManager] CompleteQuestReward failed: {e.Message}");
            var errorResponse = BuildCompleteQuestErrorResponse("CLOUD_CODE_ERROR");
            NotifyCompleteQuestRewardResult(errorResponse);
            return errorResponse;
        }
    }

    public async void CompleteQuestReward(
        CompleteQuestRewardRequest request = null,
        Action<CompleteQuestRewardResponse> callback = null)
    {
        CompleteQuestRewardResponse response = await CompleteQuestRewardAsync(request);
        callback?.Invoke(response);
    }

    private void NotifyCompleteQuestRewardResult(CompleteQuestRewardResponse response)
    {
        OnCompleteQuestRewardCompleted?.Invoke(response);
    }

    public async Task<CompleteGameRewardResponse> CompleteGameRewardAsync(CompleteGameRewardRequest request)
    {
        if (!IsValidRequest(request))
        {
            var invalidResponse = BuildCompleteGameRewardErrorResponse("INVALID_PARAMS");
            NotifyCompleteGameRewardResult(invalidResponse);
            return invalidResponse;
        }

        var parameters = new Dictionary<string, object>
        {
            { "rewardId", request.REWARD_ID }
        };

        try
        {
            CompleteGameRewardResponse response =
                await CloudCodeService.Instance.CallEndpointAsync<CompleteGameRewardResponse>(
                    CompleteGameRewardEndpoint,
                    parameters);

            if (response == null)
            {
                response = BuildCompleteGameRewardErrorResponse("EMPTY_RESPONSE");
            }

            NotifyCompleteGameRewardResult(response);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerEventManager] CompleteGameReward failed: {e.Message}");
            var errorResponse = BuildCompleteGameRewardErrorResponse("CLOUD_CODE_ERROR");
            NotifyCompleteGameRewardResult(errorResponse);
            return errorResponse;
        }
    }

    public async void CompleteGameReward(
        CompleteGameRewardRequest request = null,
        Action<CompleteGameRewardResponse> callback = null)
    {
        CompleteGameRewardResponse response = await CompleteGameRewardAsync(request);
        callback?.Invoke(response);
    }

    private void NotifyCompleteGameRewardResult(CompleteGameRewardResponse response)
    {
        OnCompleteGameRewardCompleted?.Invoke(response);
    }
}
