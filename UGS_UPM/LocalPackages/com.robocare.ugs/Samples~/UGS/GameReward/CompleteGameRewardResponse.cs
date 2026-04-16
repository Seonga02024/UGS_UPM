using System;

[Serializable]
public class CompleteGameRewardResponse
{
    public bool success;
    public string errorCode;
    public string rewardId;
    public int reward;
    public long currentMoney;
    public bool updated;
    public string message;
}
