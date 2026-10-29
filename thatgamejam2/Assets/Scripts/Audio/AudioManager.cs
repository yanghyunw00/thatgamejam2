using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Audio Manager
/// Function:
///   AudioManager.Instance.PlayBGM(clip);          // Loop Play
///   AudioManager.Instance.PlaySFX(clip);            
///   AudioManager.Instance.SetMuteAll(true);
///   AudioManager.Instance.SetBgmVolume(0.5f);
///   AudioManager.Instance.SetSfxVolume(0.5f);

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const string MuteAllKey = "audio_muteAll";
    const string BgmVolumeKey = "audio_bgmVolume";
    const string SfxVolumeKey = "audio_sfxVolume";

    [Range(0f, 1f)] public float DefaultBgmVolume = 0.7f;
    [Range(0f, 1f)] public float DefaultSfxVolume = 1f;

    AudioSource _bgmSource;
    AudioSource _sfxSource;
    AudioClip[] _sfxPool;

    public bool MuteAll { get; private set; }
    public float BgmVolume { get; private set; }
    public float SfxVolume { get; private set; }

    /// <summary>Mute/BGM/SFX 볼륨 중 하나라도 바뀌면 호출됨. Options UI 동기화용.</summary>
    public event Action SettingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("[AudioManager]").AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;

        _sfxPool = Resources.LoadAll<AudioClip>("SFX");

        LoadSettings();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Connect Every button in Scene
        foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            button.onClick.AddListener(PlayRandomSfx);
    }

    void LoadSettings()
    {
        MuteAll = PlayerPrefs.GetInt(MuteAllKey, 0) == 1;
        BgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
        Apply();
    }

    void Apply()
    {
        _bgmSource.volume = MuteAll ? 0f : BgmVolume;
        _sfxSource.volume = MuteAll ? 0f : SfxVolume;
    }

    // ───────────── Public API: 설정 ─────────────

    public void SetMuteAll(bool mute)
    {
        MuteAll = mute;
        PlayerPrefs.SetInt(MuteAllKey, mute ? 1 : 0);
        Apply();
        SettingsChanged?.Invoke();
    }

    public void SetBgmVolume(float volume)
    {
        BgmVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        Apply();
        SettingsChanged?.Invoke();
    }

    public void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        Apply();
        SettingsChanged?.Invoke();
    }

    // ───────────── Public API ─────────────

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null || _bgmSource.clip == clip) return;
        _bgmSource.clip = clip;
        _bgmSource.loop = loop;
        _bgmSource.Play();
    }

    public void StopBGM() => _bgmSource.Stop();

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        _sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// Random Play
    public void PlayRandomSfx()
    {
        if (_sfxPool == null || _sfxPool.Length == 0) return;
        PlaySFX(_sfxPool[UnityEngine.Random.Range(0, _sfxPool.Length)]);
    }
}
