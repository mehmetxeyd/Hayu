using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOver : MonoBehaviour
{
    public GameObject gameOverPanel;
    public Text highScoreText;

    void Update()
    {
        if (GameManager.gameStarted && GameObject.FindGameObjectWithTag("Player") == null)
        {
            gameOverPanel.SetActive(true);
            GameOverLogic(); 
        }
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        GameManager.gameStarted = false;
    }

    void GameOverLogic()
    {
        HighScore hs = GameObject.Find("HighScoreManager").GetComponent<HighScore>();
        hs.CheckAndSaveHighScore();
        hs.DisplayHighScore();
    }

}
