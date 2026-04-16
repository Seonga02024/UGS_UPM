using System;

[Serializable]
public class ValidateAndSetCurrentGameRequest
{
    public string HOST_ID;
    public string CLIENT_ID;
    public string SESSION_ID;
    public long HOST_MONEY;
    public long CLIENT_MONEY;
    public int GAME_POINT;
    public bool IS_AI_MODE;
}
