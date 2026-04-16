namespace RoboCare.UGS
{
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using Unity.Services.RemoteConfig;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유니티 리더보드 서비스와 통신하여 랭킹 데이터를 표시하고 업데이트하는 매니저
/// </summary>
/*
 * 사용 방법:
 * 1) Unity Dashboard Leaderboard에 leaderboardId("Ranking")를 생성합니다.
 * 2) Remote config LeaderBoardID (string) -> Ranking key 값을 생성합니다.
 * 3) rankBtn 클릭 시 GetScoresByPage가 호출되어 랭킹 패널이 열립니다.
 * 4) 점수 저장은 SavePlayerScore, 조회는 LoadAllScores/LoadPlayerRange를 사용합니다.
 */
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    #region [Fields - UI Components]

    [Header("UI Components")]
    [SerializeField] private GameObject rankPanel;
    [SerializeField] private GameObject noRankObject;
    [SerializeField] private Transform rankUserInfoParent;
    [SerializeField] private GameObject DiamondRankPrefab;
    [SerializeField] private GameObject GoldRankPrefab;
    [SerializeField] private GameObject SliverRankPrefab;
    [SerializeField] private GameObject BronzeRankPrefab;

    [Header("Control Buttons")]
    [SerializeField] private Button rankBtn;
    [SerializeField] private Button closeBtn;
    [SerializeField] private Button allScoreBtn;
    [SerializeField] private Button playerRangeBtn;
    [SerializeField] private Button prePageBtn;
    [SerializeField] private Button nextPageBtn;
    #endregion

    #region [Fields - Settings]
    private const string leaderboardIdConfigKey = "LeaderBoardID";
    [SerializeField] private string fallbackLeaderboardId = "Ranking";
    private string leaderboardId;
    private bool isLeaderboardIdLoaded = false;

    private int currentPage = 0;
    private readonly int listCount = 5;      // 페이지당 표시 개수
    private int rangeLimit = 5;             // 내 주변 순위 표시 개수
    private Dictionary<string, GameObject> rankPrefabs;
    private string currentPlayerName = "";
    private bool IsWaitLoading = false;

    #endregion

    #region [Unity Lifecycle]
    private void Awake()
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

        rankPrefabs = new Dictionary<string, GameObject> {
        { "diamond", DiamondRankPrefab },
        { "gold", GoldRankPrefab },
        { "silver", SliverRankPrefab },
        { "bronze", BronzeRankPrefab }
    };

        leaderboardId = fallbackLeaderboardId;

        BindingUIEvents();
        rankPanel.SetActive(false);
    }
    #endregion

    #region [Initialization]
    private void BindingUIEvents()
    {
        allScoreBtn.onClick.AddListener(async () => await LoadAllScores());
        playerRangeBtn.onClick.AddListener(async () => await LoadPlayerRange());
        prePageBtn.onClick.AddListener(async () => await GetScoresByPage(--currentPage));
        nextPageBtn.onClick.AddListener(async () => await GetScoresByPage(++currentPage));

        rankBtn.onClick.AddListener(async () =>
        {
            if (rankPanel.activeSelf)
            {
                noRankObject.SetActive(false);
                rankPanel.SetActive(false);
            }
            else
            {
                await GetScoresByPage(currentPage);
            }
        });

        closeBtn.onClick.AddListener(() =>
        {
            noRankObject.SetActive(false);
            rankPanel.SetActive(false);
        });
    }

    /// <summary>
    /// 점수가 없는 신규 유저를 위해 초기 점수(0점)를 등록하거나 기존 점수를 확인
    /// </summary>
    public async Task GetPlayerScoreOrSubmitDefault()
    {
        //if (LoginTokenReader.IsCamiLogin == false) return;

        var currentLeaderboardId = await GetLeaderboardIdAsync();

        try
        {
            await LeaderboardsService.Instance.GetPlayerScoreAsync(currentLeaderboardId);
        }
        catch (LeaderboardsException e)
        {
            if (e.Reason == LeaderboardsExceptionReason.EntryNotFound)
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(currentLeaderboardId, 0);
                Debug.Log("[Leaderboard] 초기 점수(0점) 등록 완료");
            }
        }
    }
    #endregion

    #region [Leaderboard Data - Get]
    private async Task<string> GetLeaderboardIdAsync()
    {
        if (isLeaderboardIdLoaded && !string.IsNullOrWhiteSpace(leaderboardId))
        {
            return leaderboardId;
        }

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
            var remoteLeaderboardId = RemoteConfigService.Instance.appConfig.GetString(leaderboardIdConfigKey, fallbackLeaderboardId);

            leaderboardId = string.IsNullOrWhiteSpace(remoteLeaderboardId) ? fallbackLeaderboardId : remoteLeaderboardId;
            isLeaderboardIdLoaded = true;
            Debug.Log($"[Leaderboard] Leaderboard ID Loaded: {leaderboardId}");
        }
        catch (Exception e)
        {
            leaderboardId = fallbackLeaderboardId;
            isLeaderboardIdLoaded = true;
            Debug.LogWarning($"[Leaderboard] Failed to load LeaderBoardID from Remote Config. fallback will be used. {e.Message}");
        }

        return leaderboardId;
    }

    /// <summary>
    /// 특정 페이지의 점수 목록을 가져와 표시
    /// </summary>
    private async Task GetScoresByPage(int page)
    {
        if (IsWaitLoading) return;
        IsWaitLoading = true;

        if (page < 0) { page = 0; currentPage = 0; }

        // 로그인 확인
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        currentPlayerName = await AuthenticationService.Instance.GetPlayerNameAsync();
        await GetPlayerScoreOrSubmitDefault();
        var currentLeaderboardId = await GetLeaderboardIdAsync();
        RemoveScoresUI();
        noRankObject.SetActive(false);

        var options = new GetScoresOptions { Offset = page * listCount, Limit = listCount };

        try
        {
            var scoresResponse = await LeaderboardsService.Instance.GetScoresAsync(currentLeaderboardId, options);
            if (scoresResponse != null && scoresResponse.Results.Count > 0)
            {
                DisplayRankItems(scoresResponse.Results);
                rankPanel.SetActive(true);
                IsWaitLoading = false;
            }
        }
        catch (LeaderboardsException e)
        {
            if (e.Reason == LeaderboardsExceptionReason.EntryNotFound || e.Message.Contains("400"))
            {
                noRankObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 내 순위를 중심으로 앞뒤 순위 데이터를 가져옴
    /// </summary>
    private async Task LoadPlayerRange()
    {
        RemoveScoresUI();
        var currentLeaderboardId = await GetLeaderboardIdAsync();
        var options = new GetPlayerRangeOptions { RangeLimit = rangeLimit };
        var response = await LeaderboardsService.Instance.GetPlayerRangeAsync(currentLeaderboardId, options);

        DisplayRankItems(response.Results);
    }

    /// <summary>
    /// 상위 30개 점수를 서버에서 가져옴
    /// </summary>
    private async Task LoadAllScores()
    {
        RemoveScoresUI();
        var currentLeaderboardId = await GetLeaderboardIdAsync();
        var options = new GetScoresOptions { Limit = 30 };
        var response = await LeaderboardsService.Instance.GetScoresAsync(currentLeaderboardId, options);

        DisplayRankItems(response.Results);
    }

    private async Task LoadScore()
    {
        var currentLeaderboardId = await GetLeaderboardIdAsync();
        var response = await LeaderboardsService.Instance.GetPlayerScoreAsync(currentLeaderboardId);
        Debug.Log($"[Leaderboard] 내 정보 - Rank: {response.Rank}, Score: {response.Score}");
    }
    #endregion

    #region [Leaderboard Data - Save]
    public async void UpdateScore(double score)
    {
        //if (LoginTokenReader.IsCamiLogin == false) return;
        await SaveScore(score);
    }

    private async Task SaveScore(double score)
    {
        var currentLeaderboardId = await GetLeaderboardIdAsync();
        var response = await LeaderboardsService.Instance.AddPlayerScoreAsync(currentLeaderboardId, score);
        Debug.Log($"[Leaderboard] 점수 저장 결과: {JsonConvert.SerializeObject(response)}");
    }
    #endregion

    #region [UI Helper Functions]
    /// <summary>
    /// 리더보드 결과 리스트를 받아서 프리팹으로 화면에 생성
    /// </summary>
    private void DisplayRankItems(List<LeaderboardEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (!rankPrefabs.TryGetValue(entry.Tier, out GameObject prefab))
            {
                prefab = BronzeRankPrefab;
            }
            var rankItem = Instantiate(prefab, rankUserInfoParent);
            if (rankItem.TryGetComponent<RankUserInfo>(out var userInfo))
            {
                userInfo.SetRankUserInfo(entry.Rank + 1, entry.PlayerName, entry.Score, entry.Tier);
                if (currentPlayerName == entry.PlayerName) userInfo.SetMyInfo(true);
                else userInfo.SetMyInfo(false);

            }
        }
        ChangeRectHeight(entries.Count);
    }

    private void RemoveScoresUI()
    {
        // 첫 번째 자식(헤더)을 제외하고 나머지 제거
        for (int i = 1; i < rankUserInfoParent.childCount; i++)
        {
            Destroy(rankUserInfoParent.GetChild(i).gameObject);
        }
    }

    private void ChangeRectHeight(int count)
    {
        float itemHeight = 30.0f; // 아이템당 높이
        float baseOffset = 210.0f; // 기본 오프셋
        float calculatedHeight = (count * itemHeight) - baseOffset;

        var contentTr = rankUserInfoParent.GetComponent<RectTransform>();
        contentTr.sizeDelta = new Vector2(contentTr.sizeDelta.x, calculatedHeight);
    }
    #endregion

    private struct UserAttributes { }
    private struct AppAttributes { }
}

}
