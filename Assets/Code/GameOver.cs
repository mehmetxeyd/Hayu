using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kept for the scene's existing Restart button. The game-over screen itself is
/// now shown by LevelManager.
/// </summary>
public class GameOver : MonoBehaviour
{
    public GameObject gameOverPanel;

    public void Restart()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.Restart(true);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
