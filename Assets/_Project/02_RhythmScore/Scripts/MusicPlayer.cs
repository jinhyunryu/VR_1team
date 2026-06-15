using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance;

    private AudioSource audioSource;

    private void Awake()
    {
        Instance = this;

        audioSource =
            GetComponent<AudioSource>();
    }

    private void Update()
    {
        Debug.Log(audioSource.time);
    }

    public float GetSongTime()
    {
        return audioSource.time;
    }
}