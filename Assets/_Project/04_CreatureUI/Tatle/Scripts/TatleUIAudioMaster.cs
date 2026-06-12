using UnityEngine;

[DisallowMultipleComponent]
public class TatleUIAudioMaster : MonoBehaviour
{
    public const string DefaultObjectName = "Tatle UI Audio Master";

    static TatleUIAudioMaster instance;

    [Header("Clips")]
    public AudioClip sceneBgm;
    public AudioClip buttonClick;
    public AudioClip buttonHover;
    public AudioClip touchToStartClick;

    [Header("Volumes")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.55f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Playback")]
    public bool playBgmOnAwake = true;
    public bool persistAcrossScenes;

    [SerializeField] AudioSource bgmSource;
    [SerializeField] AudioSource sfxSource;

    public static TatleUIAudioMaster Instance
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<TatleUIAudioMaster>(FindObjectsInactive.Include);

            return instance;
        }
    }

    public static TatleUIAudioMaster EnsureInstance()
    {
        if (Instance != null)
            return instance;

        var audioObject = new GameObject(DefaultObjectName);
        return audioObject.AddComponent<TatleUIAudioMaster>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        EnsureSources();

        if (playBgmOnAwake)
            PlaySceneBgm();
    }

    void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        bgmVolume = Mathf.Clamp01(bgmVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);

        if (bgmSource != null || sfxSource != null)
            ApplySourceSettings();
    }

    public void PlaySceneBgm()
    {
        if (sceneBgm == null)
            return;

        EnsureSources();

        if (bgmSource.clip != sceneBgm)
            bgmSource.clip = sceneBgm;

        ApplySourceSettings();

        if (!bgmSource.isPlaying)
            bgmSource.Play();
    }

    public void StopSceneBgm()
    {
        if (bgmSource != null)
            bgmSource.Stop();
    }

    public void PlayButtonClick()
    {
        PlaySfx(buttonClick);
    }

    public void PlayTouchToStartClick()
    {
        PlaySfx(touchToStartClick != null ? touchToStartClick : buttonClick);
    }

    public void PlayButtonHover()
    {
        PlaySfx(buttonHover);
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureSources();
        sfxSource.PlayOneShot(clip, masterVolume * sfxVolume);
    }

    void EnsureSources()
    {
        var sources = GetComponents<AudioSource>();

        if (bgmSource == null)
            bgmSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        if (sfxSource == null || sfxSource == bgmSource)
            sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

        ApplySourceSettings();
    }

    void ApplySourceSettings()
    {
        if (bgmSource != null)
        {
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.ignoreListenerPause = true;
            bgmSource.volume = masterVolume * bgmVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.ignoreListenerPause = true;
            sfxSource.volume = masterVolume * sfxVolume;
        }
    }
}
