namespace RoboCare.UGS
{
using System;
using System.Collections.Generic;

public class OperationInfo
{
    public string userId;
    public string userName;
    public string projectId;
    public string projectName;
    public string robotId;
    public string version;

    public string uuid;
    public override string ToString()
    {
        return $"OpertaionInfo: user:{userId}//{userName}, "
            + $"project:{projectId}//{projectName}, robotId:{robotId}, version: {version}, uuid: {uuid}";
    }
}

[Serializable]
public class GameScoreRequest
{
    public string user_id; // 사용자ID
    public string game_id; // "R03"
    public string level;   // 진행단계 {level}-{step}
    public string score;   // 점수
    public string datetime;    // 현재시간?
    public string startTime;   // 시작시간
    public string finishTime;  // 종료시간
    public string usetime;     // 사용시작 
    public string robot_id;    // 로봇 ID

    public string uuid;       // uuid 테스트
}

[Serializable]
public class GameDataRequest
{
    public List<GameScoreRequest> Data = new List<GameScoreRequest>();
}
}
