using UnityEngine;

public class GameManager :MonoBehaviour
{
    
    public GameObject startPanel;
    public GameObject player;

    public static bool gameStarted = false;

    void Start()
    {
        Time.timeScale = 0f; // Pause the game at start
        //player.SetActive(false); // Hide player until game starts
    }

    public void StartGame()
    {
        startPanel.SetActive(false); // Hide start panel
        Time.timeScale = 1f; // Resume game
        player.SetActive(true); // Enable player
        gameStarted = true;
    }
}

