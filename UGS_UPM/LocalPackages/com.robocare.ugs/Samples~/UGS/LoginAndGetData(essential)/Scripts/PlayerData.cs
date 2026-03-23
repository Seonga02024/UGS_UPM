#region [Data Models] 게임 별 커스텀 필요 
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace RoboCare.UGS
{

[Serializable]
public class PlayerData
{
    public string name = "guest";
    public int lifes = 5;
    public int Music = 1;
    public int Sound = 1;
    public int Lauched = 0;
    public int OpenLevel = 1;
    public float RestLifeTimer = 0f;
    public string DateOfExit = "";
    public int Rated = 0;
    public int GameCoin = 0;
    public List<Item> Items = new List<Item>();
    public UserQuestStatus questStatus = new UserQuestStatus();
    public List<string> completeGameRewards = new List<string>();

    public void InitPlayerData()
    {
        // 아이템 기본 세트 초기화
        Items = new List<Item>
        {
            new Item { id = ItemType.item1, count = 0 },
            new Item { id = ItemType.item2, count = 0 }
        };
    }
}

[Serializable]
public class GoldResponse
{
    public bool success;
    public int gold;
    public string message;
}

[Serializable] public class MapData { public string id; public int star; public int score; }
[Serializable] public class Item { [JsonConverter(typeof(StringEnumConverter))] public ItemType id; public int count; }
[Serializable] public class UserQuestStatus { public string lastAssignDate = ""; public List<QuestProgress> activeQuests = new List<QuestProgress>(); }
[Serializable] public class QuestProgress { public string id = ""; public string desc = ""; public string type = ""; public int goal = 0; public int reward = 0; public int currentProgress = 0; public bool isCompleted = false; }

public enum ItemType
{
    item1,
    item2
}

}

#endregion
