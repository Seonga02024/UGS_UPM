using System.Collections.Generic;
using UnityEngine;

namespace RoboCare.UGS
{
    [CreateAssetMenu(fileName = "MusicLibrary", menuName = "Audio/MusicLibrary")]
public class MusicLibrarySO : ScriptableObject
{
    [System.Serializable]
    public class MusicGroup
    {
        public string groupName; // 그룹 이름 (예: "UI", "Gameplay")
        public List<MusicEntry> musicEntries; // 해당 그룹의 음악 목록
    }

    [System.Serializable]
    public class MusicEntry
    {
        public string musicName; // 사운드의 이름 (키 역할)
        public AudioClip clip; // 사운드 클립
    }

    public List<MusicGroup> musicGroups; // 여러 그룹을 관리하는 리스트

    private Dictionary<string, Dictionary<string, AudioClip>> musicDictionary;

    public void Initialize()
    {
        musicDictionary = new Dictionary<string, Dictionary<string, AudioClip>>();
        foreach (var group in musicGroups)
        {
            if (!musicDictionary.ContainsKey(group.groupName))
            {
                musicDictionary[group.groupName] = new Dictionary<string, AudioClip>();
            }

            foreach (var entry in group.musicEntries)
            {
                if (!musicDictionary[group.groupName].ContainsKey(entry.musicName))
                {
                    musicDictionary[group.groupName].Add(entry.musicName, entry.clip);
                }
                else
                {
                    Debug.LogWarning($"Duplicate musicName found in group '{group.groupName}': {entry.musicName}. Ignoring duplicate.");
                }
            }
        }
    }

    public AudioClip GetClip(string groupName, string musicName)
    {
        if (musicDictionary != null && musicDictionary.ContainsKey(groupName) &&
            musicDictionary[groupName].ContainsKey(musicName))
        {
            return musicDictionary[groupName][musicName];
        }

        Debug.LogWarning($"Music '{musicName}' not found in group '{groupName}' or group does not exist.");
        return null;
    }
}
}
