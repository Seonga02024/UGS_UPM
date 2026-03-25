namespace RoboCare.UGS
{
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance;
    private bool isTracking = false;
    private float sessionStartTime;
    private float totalPlayTime = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // 게임 한 판을 시작할 때 호출
    public void BeginSession()
    {
        isTracking = true;
        sessionStartTime = Time.time;
        Debug.Log("[PlayerStatsManager] 플레이어 게임 플레이 시간 측정 시작!");
    }

    // 게임이 끝나거나 꺼질 때 호출
    public void EndSession()
    {
        if (!isTracking) return;
        float sessionDuration = Time.time - sessionStartTime;
        totalPlayTime += sessionDuration;
        //AddPlayTime(sessionDuration);
        isTracking = false;
        DailyQuestManager.Instance.UpdateProgress(QuestType.PlayTime, (int)(totalPlayTime/60));
        Debug.Log("[PlayerStatsManager] 플레이어 게임 플레이 시간 측정 끝!");
    }

    // --- 데이터 수정 (나중에 확장이 편리한 구조) ---

    // private void AddPlayTime(float seconds)
    // {
    //     float current = PlayerPrefs.GetFloat("Stats_TotalPlayTime", 0f);
    //     PlayerPrefs.SetFloat("Stats_TotalPlayTime", current + seconds);
    // }

    // private void AddPlayCount(int count)
    // {
    //     int current = PlayerPrefs.GetInt("Stats_TotalPlayCount", 0);
    //     PlayerPrefs.SetInt("Stats_TotalPlayCount", current + count);
    // }

    // public void IncrementCustomStat(string statKey, int amount = 1)
    // {
    //     int current = PlayerPrefs.GetInt("Stats_" + statKey, 0);
    //     PlayerPrefs.SetInt("Stats_" + statKey, current + amount);
    //     PlayerPrefs.Save();
    // }

    // // --- 데이터 로드 (CurrentPlayerData에 넣기 위해) ---

    // public void SyncToPlayerData(PlayerData data)
    // {
    //     data.totalPlayTime = PlayerPrefs.GetFloat("Stats_TotalPlayTime", 0f);
    //     data.totalPlayCount = PlayerPrefs.GetInt("Stats_TotalPlayCount", 0);
    //     // 필요한 다른 커스텀 데이터들도 여기서 동기화
    // }
}
}
