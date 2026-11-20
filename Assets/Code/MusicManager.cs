using UnityEngine;

public class MusicManager : MonoBehaviour
{
    private static MusicManager instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Keeps music between scenes
        }
        else
        {
            Destroy(gameObject); // Prevents duplicates
        }
    }
}
