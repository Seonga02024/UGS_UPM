using System.Collections.Generic;
using UnityEngine;

namespace RoboCare.UGS
{
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Audio/SoundLibrary")]
    public class SoundLibrarySO : ScriptableObject
    {
    [System.Serializable]
    public class SoundGroup
    {
        public string groupName; // 그룹 이름 (예: "UI", "Gameplay")
        public List<SoundEntry> soundEntries; // 해당 그룹의 사운드 목록
    }

    [System.Serializable]
    public class SoundEntry
    {
        public string soundName; // 사운드의 이름 (키 역할)
        public AudioClip clip; // 사운드 클립
    }

    public List<SoundGroup> soundGroups; // 여러 그룹을 관리하는 리스트

    private Dictionary<string, Dictionary<string, AudioClip>> soundDictionary;

    public void Initialize()
    {
        soundDictionary = new Dictionary<string, Dictionary<string, AudioClip>>();

        foreach (var group in soundGroups)
        {
            if (!soundDictionary.ContainsKey(group.groupName))
            {
                soundDictionary[group.groupName] = new Dictionary<string, AudioClip>();
            }

            foreach (var entry in group.soundEntries)
            {
                if (!soundDictionary[group.groupName].ContainsKey(entry.soundName))
                {
                    soundDictionary[group.groupName].Add(entry.soundName, entry.clip);
                }
                else
                {
                    Debug.LogWarning($"Duplicate soundName found in group '{group.groupName}': {entry.soundName}. Ignoring duplicate.");
                }
            }
        }
    }

    public AudioClip GetClip(string groupName, string soundName)
    {
        if (soundDictionary != null && soundDictionary.ContainsKey(groupName) &&
            soundDictionary[groupName].ContainsKey(soundName))
        {
            return soundDictionary[groupName][soundName];
        }

        LogApi.Log($"Sound '{soundName}' not found in group '{groupName}' or group does not exist.");
        return null;
    }
}
}
