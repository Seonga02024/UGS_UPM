namespace RoboCare.UGS
{
using System;

public class GameScore
{
    public string gameId {get; set;}

    public int level {get; set;}

    public float score {get; set;} 

    public DateTimeOffset startTime {get; set;} 

    public DateTimeOffset finishTime {get; set;} 

    public DateTimeOffset createdAt { get; set; } = DateTime.UtcNow;

    public string userId {get; set;} 
    public string userName {get; set;}

    public string projectId {get; set;}
    public string projectName {get; set;}

    public string robotId {get; set;} 

    public string version {get; set;}

    public bool sync {get; set;}

    public string uuid {get; set;} // uuid Test
    

    public static GameScore OfOperation(OperationInfo operation)
    {
        return new GameScore()
        {
            userId = operation.userId,
            userName = operation.userName,
            projectId = operation.projectId,
            projectName = operation.projectName,
            robotId = operation.robotId,
            version = operation.version,
            sync = false,
            uuid = operation.uuid,
        };
    }
}

}
