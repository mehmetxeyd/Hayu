using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the total number of swords dodged this run.</summary>
public class ScoreManager : MonoBehaviour
{
    public Text scoreText;

    void Update()
    {
        if (scoreText == null) return;
        var gm = GameManager.Instance;
        scoreText.text = gm != null ? gm.TotalDodged.ToString() : "0";
    }
}
