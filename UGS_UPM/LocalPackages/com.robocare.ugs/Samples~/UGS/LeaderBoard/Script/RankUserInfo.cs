namespace RoboCare.UGS
{
using TMPro;
using UnityEngine;

public class RankUserInfo : MonoBehaviour
{
    [SerializeField] private TMP_Text _rank, _playerName, _score, _tier;
    [SerializeField] private GameObject myMark;

    public void SetRankUserInfo(int rank, string playerName, double score, string tier)
    {
        _rank.text = rank.ToString();
        string originalName = playerName.Split('#')[0];
        _playerName.text = originalName.Length > 15
        ? originalName.Substring(0, 15) + "..."
        : originalName;
        _score.text = score.ToString();
        _tier.text = tier;
    }
    
    public void SetMyInfo(bool isMine)
    {
        myMark.SetActive(isMine);
    }
}

}
