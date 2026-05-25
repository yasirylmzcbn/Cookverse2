using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class UISoundManager : MonoBehaviour
{
    public static UISoundManager Instance;

    public float SfxVolume
    {
        get => audioSource != null ? audioSource.volume : PlayerPrefs.GetFloat(SoundManager.SFX_VOLUME_PREF_KEY, 1f);
        set => ApplySfxVolume(value, persist: true);
    }

    [Header("UI Sounds")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;
    [Tooltip("Sound played when opening a menu")]
    [SerializeField] private AudioClip openSound;
    [Tooltip("Sound played when closing a menu")]
    [SerializeField] private AudioClip closeSound;
    [Tooltip("Sound played when starting to drag an item")]
    [SerializeField] private AudioClip dragStartSound;
    [Tooltip("Sound played when dropping an item in a slot")]
    [SerializeField] private AudioClip dropSound;
    [Tooltip("Sound played when switching modes (cooking, difficulty, etc.)")]
    [SerializeField] private AudioClip switchSound;
    [Tooltip("Sound played when an enemy is defeated")]
    [SerializeField] private AudioClip enemyDefeatedSound;
    [Tooltip("Sound played when speed spell is cast")]
    [SerializeField] private AudioClip speedSpellSound;
    [Tooltip("Sound played when a wave ends")]
    [SerializeField] private AudioClip waveEndSound;
    [Tooltip("Sound played when the last wave finishes and portal appears")]
    [SerializeField] private AudioClip finalWaveSound;
    [Tooltip("Sound played during wave countdown (ticking)")]
    [SerializeField] private AudioClip waveTickSound;
    [Tooltip("Sound played when player jumps")]
    [SerializeField] private AudioClip jumpSound;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        // Extremely safe singleton pattern: If the user attached this to a very important scene object (like the Camera or Player),
        // we DO NOT want to apply DontDestroyOnLoad to it, because it breaks scene reloading, audio listeners, and interactions.
        // Instead, we spawn a completely invisible independent object, transfer our clips, and destroy this script from the host.
        if (gameObject.name != "GlobalUISoundManager")
        {
            GameObject bgGo = new GameObject("GlobalUISoundManager");
            DontDestroyOnLoad(bgGo);

            UISoundManager newManager = bgGo.AddComponent<UISoundManager>();
            // Transfer ALL sound clips to the new manager
            newManager.hoverSound = this.hoverSound;
            newManager.clickSound = this.clickSound;
            newManager.openSound = this.openSound;
            newManager.closeSound = this.closeSound;
            newManager.dragStartSound = this.dragStartSound;
            newManager.dropSound = this.dropSound;
            newManager.switchSound = this.switchSound;
            newManager.enemyDefeatedSound = this.enemyDefeatedSound;
            newManager.speedSpellSound = this.speedSpellSound;
            newManager.waveEndSound = this.waveEndSound;
            newManager.finalWaveSound = this.finalWaveSound;
            newManager.waveTickSound = this.waveTickSound;
            newManager.jumpSound = this.jumpSound;

            Destroy(this);
            return;
        }

        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        audioSource.ignoreListenerVolume = true;
        ApplySfxVolume(PlayerPrefs.GetFloat(SoundManager.SFX_VOLUME_PREF_KEY, 1f), persist: false);
        SoundManager.OnSfxVolumeChanged += OnSharedSfxVolumeChanged;
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        InjectSoundsIntoAllButtons();
    }

    private void OnDestroy()
    {
        SoundManager.OnSfxVolumeChanged -= OnSharedSfxVolumeChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSharedSfxVolumeChanged(float volume)
    {
        ApplySfxVolume(volume, persist: false);
    }

    private void ApplySfxVolume(float volume, bool persist)
    {
        float clamped = Mathf.Clamp01(volume);
        if (audioSource != null)
            audioSource.volume = clamped;

        if (persist)
            PlayerPrefs.SetFloat(SoundManager.SFX_VOLUME_PREF_KEY, clamped);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InjectSoundsIntoAllButtons();
    }

    public void InjectSoundsIntoAllButtons()
    {
        Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sel in selectables)
        {
            // Only modify objects in the scene, not prefabs
            if (sel.gameObject.scene.name != null && sel.GetComponent<UIButtonSound>() == null)
            {
                sel.gameObject.AddComponent<UIButtonSound>();
            }
        }
    }

    public void PlayHoverSound()
    {
        if (hoverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hoverSound);
            return;
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUIHoverSound();
        }
    }

    public void PlayClickSound()
    {
        if (clickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }

    public void PlayOpenSound()
    {
        if (openSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openSound);
        }
    }

    public void PlayCloseSound()
    {
        if (closeSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(closeSound);
        }
    }

    public void PlayDragStartSound()
    {
        if (dragStartSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dragStartSound);
        }
    }

    public void PlayDropSound()
    {
        if (dropSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dropSound);
        }
    }

    public void PlaySwitchSound()
    {
        if (switchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }

    public void PlayEnemyDefeatedSound()
    {
        if (enemyDefeatedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(enemyDefeatedSound);
        }
    }

    public void PlaySpeedSpellSound()
    {
        if (speedSpellSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(speedSpellSound);
        }
    }

    public void PlayWaveEndSound()
    {
        if (waveEndSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(waveEndSound);
        }
    }

    public void PlayFinalWaveSound()
    {
        if (finalWaveSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(finalWaveSound);
        }
    }

    public void PlayWaveTickSound()
    {
        if (waveTickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(waveTickSound);
        }
    }

    public void PlayJumpSound()
    {
        if (jumpSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }
    }
}
