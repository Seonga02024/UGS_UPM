using System;

[Serializable]
public class ClaimAttendanceRewardResponse
{
    public bool success;
    public string errorCode;
    public long currentMoney;
    public string rewardDay;
    public int reward;
    public int claimCount;
    public string monthKey;
}
