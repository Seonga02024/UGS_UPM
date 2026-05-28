namespace RoboCare.UGS
{
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/*
    ! SVP 대비 New Data 예시
    {
    "robotId" : "B1V02-001",
    "robotType" : "bomi1",
    "status" : "on",
    "data" : 
    {
        "historyId" : "834957df-ccca-44a9-89fa-bbbbbb",
        "userId" : "S123",
        "contentType" : "Bomphago",
        "contentId" :"B01",
        "contentData" :
        {
            "level" : "2",
            "score" : "68"
        },
        "startTime" : "1715063015801",
        "finishTime" : "1715063038854",
        "useTime" : "254"
    }
}
*/

[Serializable]
public class GameDataRequest_SVP
{
    public string robotId;
    public string robotType = "bomi1";
    public string status = "on";
    public List<GameData> data;
}

[Serializable]
public class GameData
{
    public string historyId;
    public string userId;
    public string contentType = "casualGame";
    public string contentId;
    public ContentData contentData;
    public string startTime;
    public string finishTime;
    public string useTime;
}

[Serializable]
public class ContentData
{
    public string level;
    public string score;
}

// GameData Response
[Serializable]
public class GameDataResponse
{
    public string code;
    public string rst;
    public string msg;
    public string errMsg;
}
}
