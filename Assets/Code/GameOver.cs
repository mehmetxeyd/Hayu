using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Restart button + high score. The game-over screen itself is shown by LevelManager,
/// which calls SaveHighScore when the run ends.
/// </summary>
public class GameOver : MonoBehaviour
{
    public GameObject gameOverPanel;
    public Text highScoreText;

    public void Restart()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.Restart(true);
            return;
        }
        GameManager.gameStarted = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Saves the score if it's a new best and returns the best score.</summary>
    public static int SaveHighScore(int score)
    {
        var hsObject = GameObject.Find("HighScoreManager");
        HighScore hs = hsObject != null ? hsObject.GetComponent<HighScore>() : null;
        if (hs == null)
        {
            int best = Mathf.Max(score, PlayerPrefs.GetInt("HighScore", 0));
            PlayerPrefs.SetInt("HighScore", best);
            PlayerPrefs.Save();
            return best;
        }
        hs.currentScore = score;
        hs.CheckAndSaveHighScore();
        if (hs.highScoreText != null) hs.DisplayHighScore();
        return hs.highScore;
    }
}
