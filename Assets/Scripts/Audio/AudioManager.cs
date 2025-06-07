using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainAudioMixer;
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioSource ambientSource;
    
    [Header("Music Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    [SerializeField] private AudioClip victoryMusic;
    
    [Header("SFX Clips")]
    [SerializeField] private AudioClip puckHitSound;
    [SerializeField] private AudioClip goalSound;
    [SerializeField] private AudioClip skatingSound;
    [SerializeField] private AudioClip shootSound;
    
    [Header("UI Clips")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip menuOpenSound;
    
    public static AudioManager Instance { get; private set; }
    
    private float masterVolume = 1.0f;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupAudioSources();
            LoadAudioSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void PlayMusic(AudioClip clip)
    {
        if (musicSource != null && clip != null)
        {
            musicSource.clip = clip;
            musicSource.Play();
        }
    }
    
    public void PlayMenuMusic() => PlayMusic(menuMusic);
    public void PlayGameMusic() => PlayMusic(gameMusic);
    public void PlayVictoryMusic() => PlayMusic(victoryMusic);
    
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
    
    public void PauseMusic()
    {
        if (musicSource != null)
            musicSource.Pause();
    }
    
    public void ResumeMusic()
    {
        if (musicSource != null)
            musicSource.UnPause();
    }
    
    // SFX Controls
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }
    
    public void PlayPuckHit() => PlaySFX(puckHitSound);
    public void PlayGoal() => PlaySFX(goalSound);
    public void PlaySkating() => PlaySFX(skatingSound);
    public void PlayShoot() => PlaySFX(shootSound);
    
    // UI Controls
    public void PlayUI(AudioClip clip)
    {
        if (uiSource != null && clip != null)
            uiSource.PlayOneShot(clip);
    }
    
    public void PlayButtonClick() => PlayUI(buttonClickSound);
    public void PlayButtonHover() => PlayUI(buttonHoverSound);
    public void PlayMenuOpen() => PlayUI(menuOpenSound);
    
    // Volume Controls (these work with your existing GameSettingsManager)
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        
        // Apply to all audio sources
        if (musicSource != null)
        {
            musicSource.volume = masterVolume;
        }
        
        if (sfxSource != null)
        {
            sfxSource.volume = masterVolume;
        }
        
        // Also set AudioListener volume as fallback
        AudioListener.volume = masterVolume;
        
        Debug.Log($"AudioManager: Set master volume to {masterVolume:F2}");
        
        // Save the setting
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.Save();
    }

    // SIMPLIFIED: Remove separate music/SFX volume controls - everything uses master volume
    public float GetMasterVolume() => masterVolume;

    private void LoadAudioSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        
        Debug.Log($"AudioManager: Loaded master volume: {masterVolume:F2}");
        
        // Apply loaded settings
        ApplyVolumeSettings();
    }

    private void ApplyVolumeSettings()
    {
        if (musicSource != null)
        {
            musicSource.volume = masterVolume;
        }
        
        if (sfxSource != null)
        {
            sfxSource.volume = masterVolume;
        }
        
        AudioListener.volume = masterVolume;
        
        Debug.Log($"AudioManager: Applied master volume settings: {masterVolume:F2}");
    }

    private void SetupAudioSources()
    {
        // Create music audio source if it doesn't exist
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicAudioSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            Debug.Log("AudioManager: Created music audio source");
        }
        
        // Create SFX audio source if it doesn't exist
        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXAudioSource");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            Debug.Log("AudioManager: Created SFX audio source");
        }
        
        // Apply current volume settings
        ApplyVolumeSettings();
    }
}
