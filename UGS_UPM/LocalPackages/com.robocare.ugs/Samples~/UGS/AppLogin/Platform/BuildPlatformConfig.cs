using UnityEngine;

[CreateAssetMenu(fileName = "BuildPlatformConfig", menuName = "Config/BuildPlatformConfig")]
public class BuildPlatformConfig : ScriptableObject
{
    public PlatformType platformType;

    [Header("로봇 타입 설정")] 
    [Tooltip("0: bomi1, 1: bomi2, 2: cami, 3: lite")] public int robotType;
    public string versionName;

    [Header("로봇별 설정")]
    [Tooltip("도구 사용 여부")] public bool useBlocks;
    [Tooltip("낚시 게임 인식 모드 활성화 여부")] public bool enableFishingGestureMode;
    [Tooltip("개인정보 약관 표시 여부")] public bool requirePrivacyConsent;
}