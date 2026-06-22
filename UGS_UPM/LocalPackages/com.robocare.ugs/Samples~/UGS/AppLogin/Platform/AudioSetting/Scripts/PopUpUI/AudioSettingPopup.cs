using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using UnityEngine.EventSystems;
using RoboCare.UGS;

public class AudioSettingPopup : PopUpUI
{
    private static Tween TweenCanvasGroupAlpha(CanvasGroup canvasGroup, float endAlpha, float duration, Ease ease)
    {
        return DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, endAlpha, duration).SetEase(ease);
    }

    [Header("Popup Animation")]
    [SerializeField] private CanvasGroup backgroundCanvasGroup;
    [SerializeField] private RectTransform popupContent;
    [SerializeField] private float showScaleDuration = 0.5f;
    [SerializeField] private float hideScaleDuration = 0.3f;
    [SerializeField] private float backgroundFadeDuration = 0.2f;
    [SerializeField] private float backgroundTargetAlpha = 0.5f;

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider fxSlider;
    [SerializeField] private Slider ttsSlider;

    [Header("Slider CanvasGroups (비활성화 시 페이드)")]
    [SerializeField] private CanvasGroup musicSliderGroup;
    [SerializeField] private CanvasGroup fxSliderGroup;
    [SerializeField] private CanvasGroup ttsSliderGroup;

    [Header("Volume Text")]
    [SerializeField] private TextMeshProUGUI masterText;
    [SerializeField] private TextMeshProUGUI musicText;
    [SerializeField] private TextMeshProUGUI fxText;
    [SerializeField] private TextMeshProUGUI ttsText;

    [Header("Buttons")]
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnSave;
    [SerializeField] private Button btnReset;
    [SerializeField] private Button btnTestTTS;
    [SerializeField] private Button btnTestFX;

    [Header("Mute Toggle")]
    [SerializeField] private Toggle toggleMute;
    [SerializeField] private GameObject muteIcon; // 음소거 시 활성화되는 아이콘

    [Header("BGM Selector")]
    public GameObject bgmSelectorRoot;

    private const string PREF_MASTER = PrefKeys.VolumeMaster;
    private const string PREF_MUSIC = PrefKeys.VolumeBgm;
    private const string PREF_FX = PrefKeys.VolumeSfx;
    private const string PREF_TTS = PrefKeys.VolumeTts;
    private const string PREF_MUTE = PrefKeys.IsMuted;

    private const float DisabledAlpha = 0.35f;
    private const float PreviewButtonCooldown = 0.2f;

    private bool isMuted = false;
    private bool isInitializing = false;
    private bool isPreviewTTSLocked = false;
    private bool isPreviewFXLocked = false;

    // 취소 시 복원용 스냅샷
    private float originalMasterVolume = 1.0f;
    private float originalMusicVolume = 1.0f;
    private float originalFxVolume = 1.0f;
    private float originalTtsVolume = 1.0f;
    private bool originalIsMuted = false;

    protected override void Awake()
    {
        base.Awake();
        InitializeButtons();
        InitializeSliders();
        InitializeMuteToggle();
    }

    private void Start()
    {
        LoadVolumeSettings();
        ShowPopupUI();
    }

    private void InitializeButtons()
    {
        // SFX/TTS 미리듣기 버튼은 슬라이더 하위에 배치되어 있어 드래그 이벤트가
        // 부모 슬라이더로 전달되면 값이 튀는 문제가 있어 입력 가드를 선적용한다.
        EnsurePreviewButtonDragGuard(btnTestTTS);
        EnsurePreviewButtonDragGuard(btnTestFX);

        if (btnClose != null)
            btnClose.onClick.AddListener(OnClickClose);

        if (btnSave != null)
            btnSave.onClick.AddListener(OnClickSave);

        if (btnReset != null)
            btnReset.onClick.AddListener(OnClickReset);

        if (btnTestTTS != null)
            btnTestTTS.onClick.AddListener(OnClickTestTTS);

        if (btnTestFX != null)
            btnTestFX.onClick.AddListener(OnClickTestFX);
    }

    private void InitializeSliders()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (fxSlider != null)
            fxSlider.onValueChanged.AddListener(OnFXVolumeChanged);

        if (ttsSlider != null)
            ttsSlider.onValueChanged.AddListener(OnTTSVolumeChanged);
    }

    private void InitializeMuteToggle()
    {
        if (toggleMute != null)
        {
            toggleMute.onValueChanged.AddListener(OnMuteToggleChanged);
        }
    }

    public override void ShowPopupUI()
    {
        RectTransform target = popupContent != null ? popupContent : transform as RectTransform;

        if (backgroundCanvasGroup != null)
        {
            backgroundCanvasGroup.DOKill();
            backgroundCanvasGroup.alpha = 0f;
            TweenCanvasGroupAlpha(backgroundCanvasGroup, backgroundTargetAlpha, backgroundFadeDuration, Ease.OutQuad);
        }

        if (target != null)
        {
            target.DOKill();
            target.localScale = Vector3.zero;
            target.DOScale(Vector3.one, showScaleDuration).SetEase(Ease.OutBack);
        }
    }

    public override void HidePopupUI()
    {
        RectTransform target = popupContent != null ? popupContent : transform as RectTransform;

        if (backgroundCanvasGroup != null)
        {
            backgroundCanvasGroup.DOKill();
            TweenCanvasGroupAlpha(backgroundCanvasGroup, 0f, backgroundFadeDuration, Ease.InQuad);
        }

        if (target != null)
        {
            target.DOKill();
            target.DOScale(Vector3.zero, hideScaleDuration).SetEase(Ease.InBack).OnComplete(() =>
            {
                //GameManager.UI.ClosePopUpUI();
                //GameManager.UI.OnAudioSettingPopupClosed();
            });
        }
        else
        {
            //GameManager.UI.ClosePopUpUI();
            //GameManager.UI.OnAudioSettingPopupClosed();
        }
    }

    private void LoadVolumeSettings()
    {
        isInitializing = true; // 초기화 시작

        // PlayerPrefs에서 로드
        float master = PlayerPrefs.GetFloat(PREF_MASTER, 1.0f);
        float music = PlayerPrefs.GetFloat(PREF_MUSIC, 0.6f);
        float fx = PlayerPrefs.GetFloat(PREF_FX, 0.6f);
        float tts = PlayerPrefs.GetFloat(PREF_TTS, 1.0f);
        bool muted = PlayerPrefs.GetInt(PREF_MUTE, 0) == 1;

        // 슬라이더 값 설정
        SetSliderWithoutNotify(masterSlider, master);
        SetSliderWithoutNotify(musicSlider, music);
        SetSliderWithoutNotify(fxSlider, fx);
        SetSliderWithoutNotify(ttsSlider, tts);

        // 텍스트 업데이트
        UpdateVolumeText(masterText, master);
        UpdateVolumeText(musicText, music);
        UpdateVolumeText(fxText, fx);
        UpdateVolumeText(ttsText, tts);

        // 음소거 토글 설정
        if (toggleMute != null)
        {
            toggleMute.SetIsOnWithoutNotify(muted);
            isMuted = muted;
        }

        CaptureOriginalState(master, music, fx, tts, muted);

        isInitializing = false; // 초기화 완료

        UpdateMuteIcon();
        UpdateSubSlidersInteractable();
        ApplyTTSMuteState();

        LogApi.Log($"AudioSettingPopup 볼륨 로드 완료: Master={master}, Music={music}, FX={fx}, TTS={tts}");
    }

    private void SetSliderWithoutNotify(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }

    private void SaveVolumeSettings()
    {
        if (masterSlider != null) PlayerPrefs.SetFloat(PREF_MASTER, masterSlider.value);
        if (musicSlider != null) PlayerPrefs.SetFloat(PREF_MUSIC, musicSlider.value);
        if (fxSlider != null) PlayerPrefs.SetFloat(PREF_FX, fxSlider.value);
        if (ttsSlider != null) PlayerPrefs.SetFloat(PREF_TTS, ttsSlider.value);

        PlayerPrefs.SetInt(PREF_MUTE, isMuted ? 1 : 0);
        PlayerPrefs.Save();

        LogApi.Log("Audio settings saved!");
    }

    #region Slider Callbacks
    private void OnMasterVolumeChanged(float value)
    {
        if (isInitializing) return; // 초기화 중에는 무시

        UpdateVolumeText(masterText, value);

        if (!isMuted && AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(value, AudioManager.AudioChannel.Master);
        }

        UpdateSubSlidersInteractable();
        ApplyTTSMuteState();
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (isInitializing) return;

        UpdateVolumeText(musicText, value);

        if (!isMuted && AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(value, AudioManager.AudioChannel.Music);
        }
    }

    private void OnFXVolumeChanged(float value)
    {
        if (isInitializing) return;

        UpdateVolumeText(fxText, value);

        if (!isMuted && AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(value, AudioManager.AudioChannel.fx);
        }
    }

    private void OnTTSVolumeChanged(float value)
    {
        if (isInitializing) return;

        UpdateVolumeText(ttsText, value);

        // if (!ShouldMuteTTS() && GameManager.TTS != null)
        // {
        //     GameManager.TTS.SetTTSVolume(value);
        // }
    }
    #endregion

    #region Mute Toggle
    private void OnMuteToggleChanged(bool isOn)
    {
        if (isInitializing) return;

        isMuted = isOn;

        if (isMuted)
        {
            SetAllVolumesToZero();
        }
        else
        {
            RestoreVolumesFromSliders();
        }

        UpdateMuteIcon();
        UpdateSubSlidersInteractable();
    }

    private void SetAllVolumesToZero()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMuteAll(true);
        }

        ApplyTTSMuteState();
    }

    private void RestoreVolumesFromSliders()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMuteAll(false);

            // 슬라이더 값으로 볼륨 복원
            if (masterSlider != null)
                AudioManager.Instance.SetVolume(masterSlider.value, AudioManager.AudioChannel.Master);
            if (musicSlider != null)
                AudioManager.Instance.SetVolume(musicSlider.value, AudioManager.AudioChannel.Music);
            if (fxSlider != null)
                AudioManager.Instance.SetVolume(fxSlider.value, AudioManager.AudioChannel.fx);
        }

        ApplyTTSMuteState();
    }

    private void UpdateMuteIcon()
    {
        if (muteIcon != null)
            muteIcon.SetActive(isMuted);
    }

    private bool ShouldMuteTTS()
    {
        bool masterZero = masterSlider != null && masterSlider.value <= 0f;
        return isMuted || masterZero;
    }

    private void ApplyTTSMuteState()
    {
        // if (GameManager.TTS == null)
        //     return;

        // bool muteTTS = ShouldMuteTTS();
        // GameManager.TTS.SetMute(muteTTS);

        // // 음소거 해제 직후 TTS 슬라이더 값을 즉시 복원
        // if (!muteTTS && ttsSlider != null)
        // {
        //     GameManager.TTS.SetTTSVolume(ttsSlider.value);
        // }
    }

    /// <summary>음소거 또는 마스터 볼륨 0%일 때 하위 슬라이더 비활성화 + 페이드</summary>
    private void UpdateSubSlidersInteractable()
    {
        bool masterZero = masterSlider != null && masterSlider.value <= 0f;
        bool enabled = !isMuted && !masterZero;
        float alpha = enabled ? 1f : DisabledAlpha;

        if (musicSlider != null) musicSlider.interactable = enabled;
        if (fxSlider != null) fxSlider.interactable = enabled;
        if (ttsSlider != null) ttsSlider.interactable = enabled;

        if (musicSliderGroup != null) musicSliderGroup.alpha = alpha;
        if (fxSliderGroup != null) fxSliderGroup.alpha = alpha;
        if (ttsSliderGroup != null) ttsSliderGroup.alpha = alpha;
    }
    #endregion

    #region Button Callbacks
    private void OnClickClose()
    {
        // if (AudioManager.Instance != null)
        //     AudioManager.Instance.PlayButtonClick();

        StopTestTTS();

        // 저장하지 않고 닫을 때는 팝업 열기 전 상태로 즉시 원복
        RestoreOriginalState();
        HidePopupUI();
    }

    private void OnClickSave()
    {
        // if (AudioManager.Instance != null)
        //     AudioManager.Instance.PlayButtonClick();

        StopTestTTS();

        SaveVolumeSettings();
        HidePopupUI();
    }

    private void OnClickReset()
    {
        // if (AudioManager.Instance != null)
            //AudioManager.Instance.PlayButtonClick();

        isInitializing = true; // 리셋 중에는 리스너 무시

        // 기본값 — Master/TTS는 100%, 다른 음원(BGM/SFX)는 60% (TTS 발화가 다른 음원에 묻히지 않도록 상대 boost)
        const float defaultMaster = 1.0f;
        const float defaultMusic = 0.6f;
        const float defaultFx = 0.6f;
        const float defaultTts = 1.0f;

        SetSliderWithoutNotify(masterSlider, defaultMaster);
        SetSliderWithoutNotify(musicSlider, defaultMusic);
        SetSliderWithoutNotify(fxSlider, defaultFx);
        SetSliderWithoutNotify(ttsSlider, defaultTts);

        // 음소거 해제
        if (toggleMute != null)
        {
            toggleMute.SetIsOnWithoutNotify(false);
            isMuted = false;
        }

        // 텍스트 업데이트
        UpdateVolumeText(masterText, defaultMaster);
        UpdateVolumeText(musicText, defaultMusic);
        UpdateVolumeText(fxText, defaultFx);
        UpdateVolumeText(ttsText, defaultTts);

        isInitializing = false;

        UpdateMuteIcon();
        UpdateSubSlidersInteractable();

        // 매니저에 적용
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMuteAll(false);
            AudioManager.Instance.SetVolume(defaultMaster, AudioManager.AudioChannel.Master);
            AudioManager.Instance.SetVolume(defaultMusic, AudioManager.AudioChannel.Music);
            AudioManager.Instance.SetVolume(defaultFx, AudioManager.AudioChannel.fx);
        }

        // if (GameManager.TTS != null)
        //     GameManager.TTS.SetTTSVolume(defaultTts);

        ApplyTTSMuteState();
    }

    static readonly string[] kTtsTestDigits = { "영", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구", "십" };
    int ttsTestIndex = -1;
    private void OnClickTestTTS()
    {
        if (isPreviewTTSLocked)
            return;

        LockPreviewTTSButton();
        SyncRuntimeVolumeFromCurrentUI();

        // if (AudioManager.Instance != null)
        //     AudioManager.Instance.PlayButtonClick();

        // // 미리듣기 연타 시 TTS 중복 재생으로 체감 볼륨이 튀는 현상을 방지한다.
        // if (!ShouldMuteTTS() && GameManager.TTS != null)
        // {
        //     if (ttsTestIndex >= 0) return;
        //     ttsTestIndex = 0;
        //     GameManager.TTS.stop();
        //     SpeakNextTestDigit();
        // }
    }

    void SpeakNextTestDigit()
    {
        if (ttsTestIndex < 0 || ttsTestIndex >= kTtsTestDigits.Length)
        {
            ttsTestIndex = -1;
            return;
        }
        string digit = kTtsTestDigits[ttsTestIndex++];
        //GameManager.TTS.start(digit, SpeakNextTestDigit);
    }

    private void StopTestTTS()
    {
        // 청킹 체인을 먼저 끊어서 stop() 이후 SpeakNextTestDigit이 다시 호출되지 않도록 한다.
        ttsTestIndex = -1;

        // if (GameManager.TTS != null)
        //     GameManager.TTS.stop();
    }

    private void OnClickTestFX()
    {
        if (isPreviewFXLocked)
            return;

        LockPreviewFXButton();
        SyncRuntimeVolumeFromCurrentUI();

        // if (AudioManager.Instance != null)
        //     AudioManager.Instance.PlayButtonClick();
    }
    #endregion

    private void LockPreviewTTSButton()
    {
        isPreviewTTSLocked = true;

        if (btnTestTTS != null)
            btnTestTTS.interactable = false;

        DOVirtual.DelayedCall(PreviewButtonCooldown, () =>
        {
            isPreviewTTSLocked = false;

            if (btnTestTTS != null)
                btnTestTTS.interactable = true;
        });
    }

    private void LockPreviewFXButton()
    {
        isPreviewFXLocked = true;

        if (btnTestFX != null)
            btnTestFX.interactable = false;

        DOVirtual.DelayedCall(PreviewButtonCooldown, () =>
        {
            isPreviewFXLocked = false;

            if (btnTestFX != null)
                btnTestFX.interactable = true;
        });
    }

    private void SyncRuntimeVolumeFromCurrentUI()
    {
        if (AudioManager.Instance == null)
            return;

        if (isMuted)
        {
            AudioManager.Instance.SetMuteAll(true);
            ApplyTTSMuteState();
            return;
        }

        AudioManager.Instance.SetMuteAll(false);

        if (masterSlider != null)
            AudioManager.Instance.SetVolume(masterSlider.value, AudioManager.AudioChannel.Master);
        if (musicSlider != null)
            AudioManager.Instance.SetVolume(musicSlider.value, AudioManager.AudioChannel.Music);
        if (fxSlider != null)
            AudioManager.Instance.SetVolume(fxSlider.value, AudioManager.AudioChannel.fx);

        ApplyTTSMuteState();
    }

    private void EnsurePreviewButtonDragGuard(Button previewButton)
    {
        if (previewButton == null)
            return;

        if (previewButton.GetComponent<PreviewButtonDragGuard>() == null)
        {
            previewButton.gameObject.AddComponent<PreviewButtonDragGuard>();
        }
    }

    /// <summary>
    /// 슬라이더 하위 버튼에서 발생하는 드래그 이벤트가 부모 Slider로 전달되지 않도록 차단한다.
    /// </summary>
    private sealed class PreviewButtonDragGuard : MonoBehaviour,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public void OnInitializePotentialDrag(PointerEventData eventData) { }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { }
        public void OnEndDrag(PointerEventData eventData) { }
    }

    private void UpdateVolumeText(TextMeshProUGUI text, float value)
    {
        if (text != null)
        {
            text.text = Mathf.RoundToInt(value * 100) + "%";
        }
    }

    private void OnDestroy()
    {
        // 리스너 제거
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (fxSlider != null) fxSlider.onValueChanged.RemoveListener(OnFXVolumeChanged);
        if (ttsSlider != null) ttsSlider.onValueChanged.RemoveListener(OnTTSVolumeChanged);

        if (toggleMute != null) toggleMute.onValueChanged.RemoveListener(OnMuteToggleChanged);

        if (btnClose != null) btnClose.onClick.RemoveListener(OnClickClose);
        if (btnSave != null) btnSave.onClick.RemoveListener(OnClickSave);
        if (btnReset != null) btnReset.onClick.RemoveListener(OnClickReset);
        if (btnTestTTS != null) btnTestTTS.onClick.RemoveListener(OnClickTestTTS);
        if (btnTestFX != null) btnTestFX.onClick.RemoveListener(OnClickTestFX);
    }

    private void CaptureOriginalState(float master, float music, float fx, float tts, bool muted)
    {
        originalMasterVolume = master;
        originalMusicVolume = music;
        originalFxVolume = fx;
        originalTtsVolume = tts;
        originalIsMuted = muted;
    }

    private void RestoreOriginalState()
    {
        isInitializing = true;

        SetSliderWithoutNotify(masterSlider, originalMasterVolume);
        SetSliderWithoutNotify(musicSlider, originalMusicVolume);
        SetSliderWithoutNotify(fxSlider, originalFxVolume);
        SetSliderWithoutNotify(ttsSlider, originalTtsVolume);

        if (toggleMute != null)
            toggleMute.SetIsOnWithoutNotify(originalIsMuted);

        isMuted = originalIsMuted;

        UpdateVolumeText(masterText, originalMasterVolume);
        UpdateVolumeText(musicText, originalMusicVolume);
        UpdateVolumeText(fxText, originalFxVolume);
        UpdateVolumeText(ttsText, originalTtsVolume);

        if (AudioManager.Instance != null)
        {
            // mute 상태와 볼륨값을 모두 열기 전 상태로 복원
            AudioManager.Instance.SetVolume(originalMasterVolume, AudioManager.AudioChannel.Master);
            AudioManager.Instance.SetVolume(originalMusicVolume, AudioManager.AudioChannel.Music);
            AudioManager.Instance.SetVolume(originalFxVolume, AudioManager.AudioChannel.fx);
            AudioManager.Instance.SetMuteAll(originalIsMuted);
        }

        ApplyTTSMuteState();

        isInitializing = false;
    }
}
