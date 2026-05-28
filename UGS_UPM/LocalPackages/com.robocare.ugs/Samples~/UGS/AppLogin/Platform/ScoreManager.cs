using System;
using System.Collections.Generic;
using RoboCare.UGS;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    private GameScore m_Game;           // 게임 점수 정보
    private OperationInfo m_Operation;  // 사용자 정보
    private HibomiApiClient api;        // Hibomi api
    public bool onGetUserData = false;

    private void Awake()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        DontDestroyOnLoad(gameObject); // Optional
    }

    private void Start()
    {
        api = new HibomiApiClient();
        Setup();
    }

    public void GetUserData()
    {
        onGetUserData = PlayerPrefs.GetString(PrefKeys.GetUserData).Equals("on");

        if (onGetUserData)
        {
            _ = HibomiApiClient.Instance().GetRobotID();
            LogApi.Log("신원인식 기능 On 상태, UserData 가져옴");
        }
        else
            LogApi.Log("신원인식 기능 Off 상태, UserData 가져오지 않음");

        //_ = HibomiApiClient.Instance().GetCurrentUser();
    }

    // initApi - 초기화
    // Setup의 경우 API로 사용자 정보를 받아왔을 때 실행시켜 PlayerPrefs의 정보를 바꿀 수 있도록 함
    public void Setup()
    {
        // _ = HibomiApiClient.Instance().GetRobotID();
        GetUserData();

        m_Operation = new OperationInfo
        {
            userId = PlayerPrefs.GetString(PrefKeys.User),
            userName = PlayerPrefs.GetString(PrefKeys.UserName),
            projectId = PlayerPrefs.GetString(PrefKeys.PrjId),
            projectName = PlayerPrefs.GetString(PrefKeys.PrjType),
            robotId = PlayerPrefs.GetString(PrefKeys.RobotId) ?? "default-user",
            version = PlayerPrefs.GetString(PrefKeys.Version),
            uuid = PlayerPrefs.GetString(PrefKeys.Uuid),
        };

        LogApi.Log($"Operation: {m_Operation}");
    }

    /// init - 문제제출 시 마다 (레벨, 단계, 시작시간)
    public void SetGame(string gameId, int level)
    {
        m_Game = GameScore.OfOperation(m_Operation);
        m_Game.gameId = gameId;
        m_Game.level = level;
        m_Game.startTime = DateTimeOffset.UtcNow;
    }


    /// startGame - 게임시작 버튼 클릭 시 호출
    public void StartGame()
    {

    }

    public void FinishGame(int score)
    {
        m_Game.score = score;
        m_Game.finishTime = DateTimeOffset.UtcNow;

        if (PlayerPrefs.GetString(PrefKeys.ScoreMode) == "api")
            _ = api.SendGameScore(MakeData(m_Game));
        else
            LogApi.Log("not scoremode");
    }

    // Server에 전송할 데이터 양식을 만드는 함수
    public List<GameScore> MakeData(GameScore m_Game)
    {
        List<GameScore> list = new List<GameScore>();

        var operation = new OperationInfo
        {
            userId = PlayerPrefs.GetString(PrefKeys.User),
            userName = PlayerPrefs.GetString(PrefKeys.UserName),
            projectId = PlayerPrefs.GetString(PrefKeys.PrjId),
            projectName = PlayerPrefs.GetString(PrefKeys.PrjType),
            robotId = PlayerPrefs.GetString(PrefKeys.RobotId),
            version = PlayerPrefs.GetString(PrefKeys.Version),
            uuid = PlayerPrefs.GetString(PrefKeys.Uuid),
        };

        var item = GameScore.OfOperation(operation);
        item.gameId = m_Game.gameId;
        item.level = m_Game.level;
        item.startTime = m_Game.startTime;
        item.finishTime = m_Game.finishTime;
        item.score = m_Game.score;

        list.Add(item);
        LogApi.Log($"{item.gameId} {item.level} {item.score} {item.uuid} ");

        return list;
    }

        // guest모드 수정
    public void SetGuestMode(bool isGuest)
    {
        if (isGuest)
        {
            LogApi.LogWarning($"{GetType()}::Guest Mode 입니다.");
            PlayerPrefs.SetString(PrefKeys.ScoreMode, "android");
            PlayerPrefs.SetString(PrefKeys.RobotId, "guest");
            PlayerPrefs.SetString(PrefKeys.UserName, "guest");
        }
        else
        {
            LogApi.Log($"{GetType()}::Guest Mode 아닙니다.");
            PlayerPrefs.SetString(PrefKeys.ScoreMode, "api");
        }

        PlayerPrefs.Save();
    }
}
