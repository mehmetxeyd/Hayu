using UnityEngine;
using UnityEngine.UI;

public class HighScore : MonoBehaviour
{
    public int currentScore;
    public int highScore;
    public Text highScoreText;

    void Start()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }

    public void CheckAndSaveHighScore()
    {
        if (currentScore > highScore)
        {
            highScore = currentScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
        }
    }

    public void DisplayHighScore()
    {
        highScoreText.text = "High Score: " + highScore.ToString();
    }
}
