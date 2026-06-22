using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace RoboCare.UGS
{
public class AudioManager : MonoBehaviour
{
	public enum AudioChannel { Master, Music, fx, Tts };
	private static AudioManager instance;
	public static AudioManager Instance => instance;

	[Header("Volume Range")]
	[Range(0,1)]
	public float masterVolume = 1f; // Overall volume
	[Range(0,1)]
	public float musicVolume = 0.6f; // Music volume
	[Range(0,1)]
	public float sfxVolume = 0.6f; // FX volume
	[Range(0,1)]
	public float ttsVolume = 1f; // TTS volume
	public float fxVolume { get => sfxVolume; set => sfxVolume = value; } // alias

	public bool isMuted { get; private set; }
	public int selectedBgmIndex { get; private set; }

	public bool MusicIsLooping = true;
	public string currentBGM = "";

	//==============================================================
	// Seperate audiosources
	//==============================================================
	[Header("Audio Sources")]
	public AudioSource musicSource;
	public AudioSource sfxSource;
	public AudioSource ttsSource; // Supertonic2 TTS 발화 — 외부 접근 허용 (TTSManager가 사용)

	[Header("Audio Mixer (TTS 전용)")]
	[Tooltip("TTS 발화 채널의 출력 Mixer Group. 비워두면 기본 라우팅. RobocareAudio.mixer의 TTS Group을 +6dB로 사용 권장.")]
	public AudioMixerGroup ttsMixerGroup;

	[Range(0, 1)]
	public float duckingVolume = 0.1f; // BGM 덕킹 시 musicVolume에 곱하는 계수

	//==============================================================
	// Sound libraries. All your audio clips
	//==============================================================
	[Header("Audio Library Scriptable Object")]
	public MusicLibrarySO musicLibrarySO;
	public SoundLibrarySO soundLibrarySO;

	private Dictionary<AudioClip, int> playingSounds = new Dictionary<AudioClip, int>();
    private const int maxSimultaneousSounds = 7;
	private CancellationTokenSource cancellationTokenSource;

	//==============================================================
	// Awake
	//==============================================================
	private void Awake()
	{
		if (instance != null)
        {
            Destroy(this);
            return;
        }
        instance = this;
		DontDestroyOnLoad(gameObject);

		InitializeAudioSources();
		InitializeSoundResource();
		InitializeMuteSetting();
		InitializeVolumes();
	}

	private void InitializeSoundResource()
	{
		musicLibrarySO.Initialize();
		soundLibrarySO.Initialize();
	}

    private AudioSource CreateAudioSource(string name, bool loop = false)
    {
        GameObject newSource = new GameObject(name);
        AudioSource source = newSource.AddComponent<AudioSource>();
        newSource.transform.SetParent(transform);
        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }

	private void InitializeAudioSources()
    {
        musicSource = CreateAudioSource("Music source", true);
        sfxSource = CreateAudioSource("2D fx source");
        ttsSource = CreateAudioSource("TTS source");
        ttsSource.spatialBlend = 0f;
        if (ttsMixerGroup != null)
            ttsSource.outputAudioMixerGroup = ttsMixerGroup;
    }

	private void InitializeMuteSetting()
	{
		if (PlayerPrefs.HasKey(PrefKeys.MusicOnLegacy))
		{
			float value = PlayerPrefs.GetFloat(PrefKeys.MusicOnLegacy);
			if (value == 1)
				MuteMusic(false);
			else if (value == 0)
				MuteMusic(true);
		}
		else
			MuteMusic(false);

		if (PlayerPrefs.HasKey(PrefKeys.SoundOnLegacy))
		{
			float value = PlayerPrefs.GetFloat(PrefKeys.SoundOnLegacy);
			if (value == 1)
				MuteSound(false);
			else if (value == 0)
				MuteSound(true);
		}
		else
			MuteSound(false);
	}

	public void InitializeVolumes()
	{
		if (PlayerPrefs.HasKey(PrefKeys.MasterVolumeLegacy))
			SetVolume(PlayerPrefs.GetFloat(PrefKeys.MasterVolumeLegacy), AudioChannel.Master);	
		else
			SetVolume(masterVolume, AudioChannel.Master);
		
		if (PlayerPrefs.HasKey(PrefKeys.MusicVolumeLegacy))
			SetVolume(PlayerPrefs.GetFloat(PrefKeys.MusicVolumeLegacy), AudioChannel.Music);
		else
			SetVolume(musicVolume, AudioChannel.Music);

		if (PlayerPrefs.HasKey(PrefKeys.SoundVolumeLegacy))
			SetVolume(PlayerPrefs.GetFloat(PrefKeys.SoundVolumeLegacy), AudioChannel.fx);
		else
			SetVolume(sfxVolume, AudioChannel.fx);

		if (PlayerPrefs.HasKey(PrefKeys.VolumeTts))
			SetVolume(PlayerPrefs.GetFloat(PrefKeys.VolumeTts), AudioChannel.Tts);
		else
			SetVolume(ttsVolume, AudioChannel.Tts);
	}

	//==============================================================
	// Volumes Events
	//==============================================================
	private bool isMusicMuted = false;
	public bool IsMusicMuted => isMusicMuted;
    private bool isSoundMuted = false;
	public bool IsSoundMuted => isSoundMuted;

	public void MuteMusic(bool isMute)
    {
        if (isMute)
		{
			isMusicMuted = true;
			PlayerPrefs.SetFloat(PrefKeys.MusicOnLegacy, 0);
			musicSource.mute = true;
		}
		else
		{
			isMusicMuted = false;
			PlayerPrefs.SetFloat(PrefKeys.MusicOnLegacy, 1);
			musicSource.mute = false;
		}
    }

    public void MuteSound(bool isMute)
    {
        if (isMute)
		{
			isSoundMuted = true;
			PlayerPrefs.SetFloat(PrefKeys.SoundOnLegacy, 0);
			sfxSource.mute = true;
		}
		else
		{
			isSoundMuted = false;
			PlayerPrefs.SetFloat(PrefKeys.SoundOnLegacy, 1);
			sfxSource.mute = false;
		}
    }

	//==============================================================
	// Set volume on all the channels
	//==============================================================
	public void SetVolume(float volumePercent, AudioChannel channel)
	{
		switch (channel)
		{
			case AudioChannel.Master:
				masterVolume = volumePercent;
				break;
			case AudioChannel.Music:
				musicVolume = volumePercent;
				break;
			case AudioChannel.fx:
				sfxVolume = volumePercent;
				break;
			case AudioChannel.Tts:
				ttsVolume = volumePercent;
				break;
		}

		// Set the audiosource volume
		ApplyVolumesToSources();

		SetPlayerPrefsCurrentVolume();
	}

	private void ApplyVolumesToSources()
	{
		if (musicSource != null)
			musicSource.volume = musicVolume * masterVolume * (isDuckedNow ? duckingVolume : 1f);
		if (sfxSource != null)
			sfxSource.volume = sfxVolume * masterVolume;
		if (ttsSource != null)
			ttsSource.volume = ttsVolume * masterVolume; // TTS 자신이 덕킹 트리거이므로 ducked 대상 X
	}

	private void SetPlayerPrefsCurrentVolume()
	{
		PlayerPrefs.SetFloat(PrefKeys.MasterVolumeLegacy, masterVolume);
		PlayerPrefs.SetFloat(PrefKeys.MusicVolumeLegacy, musicVolume);
		PlayerPrefs.SetFloat(PrefKeys.SfxVolumeLegacy, sfxVolume);
		PlayerPrefs.SetFloat(PrefKeys.VolumeTts, ttsVolume);
	}

	//==============================================================
	// Play music with delay. 0 = No delay
	//==============================================================
	public void PlayMusic(string groupName, string musicName, float delay = 0f)
	{
		AudioClip clip = musicLibrarySO.GetClip(groupName, musicName);
		if (clip == null)
		{
			Debug.LogWarning($"Music clip '{musicName}' not found in the library.");
			return;
		}
		currentBGM = musicName;
		musicSource.clip = clip;
		musicSource.PlayDelayed(delay);
	}

	//==============================================================
	// Play music fade in
	//==============================================================
	public void StopAllAudioTasks()
	{
		cancellationTokenSource?.Cancel();
	}

	public void PlayMusicFade(string groupName, string musicName, float duration)
	{
		StopAllAudioTasks();
		cancellationTokenSource = new CancellationTokenSource();
		PlayMusicFadeTask(groupName, musicName, duration, cancellationTokenSource.Token).Forget();
	}

	public async UniTaskVoid PlayMusicFadeTask(string groupName, string musicName, float duration, CancellationToken cancellationToken)
    {
		AudioClip clip = musicLibrarySO.GetClip(groupName, musicName);
        if (clip == null)
		{
			Debug.LogWarning($"Music clip '{musicName}' not found in the library.");
			return;
		}

		float targetVolume = musicVolume * masterVolume;
		float fadeOutDuration = 0.5f; // 이전 BGM 페이드아웃

		// 이전 BGM이 재생 중이면 페이드아웃
		if (musicSource.isPlaying)
		{
			float startVol = musicSource.volume;
			float t = 0f;
			while (t < fadeOutDuration)
			{
				if (cancellationToken.IsCancellationRequested) return;
				t += Time.unscaledDeltaTime;
				musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeOutDuration);
				await UniTask.Yield();
			}
			musicSource.Stop();
		}

		// 새 BGM 페이드인
		currentBGM = musicName;
		musicSource.clip = clip;
		musicSource.volume = 0f;
        musicSource.Play();

		float currentTime = 0f;
        while (currentTime < duration)
		{
			if (cancellationToken.IsCancellationRequested) break;
			currentTime += Time.unscaledDeltaTime;
			musicSource.volume = Mathf.Lerp(0f, targetVolume, currentTime / duration);
			await UniTask.Yield();
		}
		musicSource.volume = targetVolume;
    }

	//==============================================================
	// Stop music
	//==============================================================
	public void StopMusic()
	{
		musicSource.Stop();
	}

	//==============================================================
	// Stop music fade out
	//==============================================================
	public void StopMusicFade(float duration)
    {
        StopMusicFadeTask(duration).Forget();
    }

    public async UniTaskVoid StopMusicFadeTask(float duration)
    {
        float currentVolume = musicSource.volume;
        float startVolume = musicSource.volume;
        float targetVolume = 0;
        float currentTime = 0;

        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / duration);
            await UniTask.Yield();
        }
        musicSource.Stop();
        musicSource.volume = currentVolume;
    }

	//==============================================================
	// FX Audio
	//==============================================================
	public void PlaySound2D(AudioClip clip)
    {
        PlaySoundInternal(clip, sfxSource);
    }

    public void PlaySound2D(string groupName, string soundName)
    {
		AudioClip clip = soundLibrarySO.GetClip(groupName, soundName);
		if(clip == null)
		{
			LogApi.Log($"{soundName} Clip is NULL");
			return;
		}

        PlaySoundInternal(clip, sfxSource);
    }

    public void PlaySound3D(string groupName, string soundName, Vector3 soundPosition)
    {
        PlaySoundInternal(soundLibrarySO.GetClip(groupName, soundName), sfxSource, soundPosition);
    }

    private void PlaySoundInternal(AudioClip clip, AudioSource source, Vector3? position = null)
	{
		if (!playingSounds.ContainsKey(clip))
		{
			playingSounds[clip] = 0;
		}

		if (playingSounds[clip] >= maxSimultaneousSounds)
		{
			return;
		}

		playingSounds[clip]++;
		if (position.HasValue)
		{
			AudioSource.PlayClipAtPoint(clip, position.Value, sfxVolume * masterVolume);
		}
		else
		{
			source.PlayOneShot(clip, source.volume);
		}
		StartSoundPlayCounter(clip);
	}

    private async void StartSoundPlayCounter(AudioClip clip)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(clip.length * 0.7f));
        playingSounds[clip]--;
    }

	//==============================================================
	// Sound Extension
	//==============================================================
	public void SetMusicVolumeTtsSpeaking()
	{
		SetVolume(0.2f, AudioChannel.Music);
	}

	public void SetMusicMute()
	{
		SetVolume(0f, AudioChannel.Music);
	}

	public void ResetMusicVolumeTtsSpeaking()
	{
		SetVolume(0.9f, AudioChannel.Music);
	}

	public void SetVolumeTtsSpeaking()
	{
		SetVolume(0.3f, AudioChannel.Master);
	}

	public void ResetVolumeTtsSpeaking()
	{
		SetVolume(1f, AudioChannel.Master);
	}

	//==============================================================
	// GameSettingUI Support
	//==============================================================
	public void SetMuteAll(bool mute)
	{
		isMuted = mute;
		if (musicSource != null) musicSource.mute = mute;
		if (sfxSource != null) sfxSource.mute = mute;
		if (ttsSource != null) ttsSource.mute = mute;
	}

	//==============================================================
	// Ducking — 토큰 기반 BGM 덕킹 (TTS 발화 시 BGM 자동 감소)
	//==============================================================
	// 사용법:
	//   int token = AudioManager.Instance.BeginDuck(clip.length + 1f);
	//   ... 음성 재생 ...
	//   AudioManager.Instance.EndDuck(token);
	//
	// 안전 장치: maxDuration 후 자동 만료 (caller가 EndDuck 잊어도 BGM 복원)
	private struct DuckEntry
	{
		public int token;
		public float endTime; // Time.unscaledTime 기준 만료
	}
	private readonly List<DuckEntry> activeDucks = new List<DuckEntry>();
	private int nextDuckToken = 1;
	private bool isDuckedNow = false;

	public int BeginDuck(float maxDuration)
	{
		if (maxDuration <= 0f) maxDuration = 0.1f;
		int token = nextDuckToken++;
		activeDucks.Add(new DuckEntry { token = token, endTime = Time.unscaledTime + maxDuration });
		UpdateDuckState();
		return token;
	}

	public void EndDuck(int token)
	{
		if (token <= 0) return;
		int removed = activeDucks.RemoveAll(e => e.token == token);
		if (removed > 0) UpdateDuckState();
	}

	private void Update()
	{
		if (activeDucks.Count == 0) return;
		float now = Time.unscaledTime;
		bool changed = false;
		for (int i = activeDucks.Count - 1; i >= 0; i--)
		{
			if (activeDucks[i].endTime <= now)
			{
				activeDucks.RemoveAt(i);
				changed = true;
			}
		}
		if (changed) UpdateDuckState();
	}

	private void UpdateDuckState()
	{
		bool shouldDuck = activeDucks.Count > 0;
		if (shouldDuck == isDuckedNow) return;
		isDuckedNow = shouldDuck;
		ApplyVolumesToSources();
	}

	public void PlaySound2D(string soundName)
	{
		PlaySound2D("UI", soundName);
	}

	public string[] GetMusicNames()
	{
		if (musicLibrarySO == null) return new string[0];
		var names = new List<string>();
		foreach (var group in musicLibrarySO.musicGroups)
			foreach (var entry in group.musicEntries)
				names.Add(entry.musicName);
		return names.ToArray();
	}

	public void ChangeBgm(int index)
	{
		var names = GetMusicNames();
		if (index < 0 || index >= names.Length) return;
		selectedBgmIndex = index;
		PlayerPrefs.SetInt("SelectedBgmIndex", index);
		// 현재 재생 중이면 전환
		if (musicSource.isPlaying)
		{
			string name = names[index];
			// 그룹 검색
			foreach (var group in musicLibrarySO.musicGroups)
				foreach (var entry in group.musicEntries)
					if (entry.musicName == name)
					{
						PlayMusicFade(group.groupName, name, 0.5f);
						return;
					}
		}
	}
}
}
