using System;

[Serializable]
public class CompleteQuestRewardResponse
{
    public bool success;
    public string errorCode;
    public string questId;
    public int reward;
    public long currentMoney;
    public bool updated;
    public string message;
}
