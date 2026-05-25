using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using System;

/// <summary>
/// Centralized sound manager for all game audio.
/// All sound effects are defined here for easy access and management.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    public const string SFX_VOLUME_PREF_KEY = "Cookverse_SfxVolume";
    public static event Action<float> OnSfxVolumeChanged;

    [Header("Player Sounds")]
    [Tooltip("Sound played when the player takes damage")]
    [FormerlySerializedAs("playerHitSound")]
    [SerializeField] private AudioClip playerDamagedSound;
    [Tooltip("Sound played when the player reaches 0 health")]
    [SerializeField] private AudioClip playerDefeatedSound;
    [Tooltip("Sound played when the player walks")]
    [SerializeField] private AudioClip footstepSound;
    [Tooltip("Sound played when the player jumps")]
    [SerializeField] private AudioClip jumpSound;

    [Header("Enemy Sounds")]
    [Tooltip("Sound played when an enemy attacks")]
    [SerializeField] private AudioClip enemyAttackSound;
    [Tooltip("Sound played when an enemy takes damage")]
    [FormerlySerializedAs("enemyHitSound")]
    [SerializeField] private AudioClip enemyDamagedSound;
    [Tooltip("Sound played when an enemy is defeated")]
    [SerializeField] private AudioClip enemyDefeatedSound;

    [Header("UI Sounds")]
    [Tooltip("Sound played when hovering over a UI element")]
    [SerializeField] private AudioClip uiHoverSound;
    [Tooltip("Sound played when clicking a UI button")]
    [SerializeField] private AudioClip uiClickSound;
    [Tooltip("Sound played when opening a menu")]
    [SerializeField] private AudioClip uiOpenSound;
    [Tooltip("Sound played when closing a menu")]
    [SerializeField] private AudioClip uiCloseSound;
    [Tooltip("Sound played when starting to drag an item")]
    [SerializeField] private AudioClip uiDragStartSound;
    [Tooltip("Sound played when dropping an item in a slot")]
    [SerializeField] private AudioClip uiDropSound;
    [Tooltip("Sound played when switching modes (cooking, difficulty, etc.)")]
    [SerializeField] private AudioClip uiSwitchSound;

    [Header("Wave Sounds")]
    [Tooltip("Sound played when a new wave starts")]
    [SerializeField] private AudioClip waveStartSound;
    [Tooltip("Sound played during wave countdown (ticking)")]
    [SerializeField] private AudioClip waveTickSound;
    [Tooltip("Sound played when a wave ends")]
    [SerializeField] private AudioClip waveEndSound;
    [Tooltip("Sound played when the last wave finishes and portal appears")]
    [SerializeField] private AudioClip waveFinalSound;

    [Header("Spell Sounds")]
    [Tooltip("Sound played when speed spell is cast")]
    [SerializeField] private AudioClip spellSpeedSound;
    [Tooltip("Sound played when the speed spell boost ends")]
    [SerializeField] private AudioClip spellSpeedEndSound;
    [Tooltip("Sound played when the heal spell is cast")]
    [SerializeField] private AudioClip spellHealSound;
    [Tooltip("Sound played when the projectile spell is cast")]
    [SerializeField] private AudioClip spellProjectileSound;
    [Tooltip("Sound played when a projectile hits something and disappears")]
    [SerializeField] private AudioClip spellProjectileImpactSound;
    [Tooltip("Sound played when the lightning/stun spell is cast")]
    [SerializeField] private AudioClip spellStunSound;
    [Tooltip("Sound played when the ground slam spell is cast")]
    [SerializeField] private AudioClip spellAoEDamageSound;
    [Tooltip("Sound played when the forcefield/knockback spell is cast")]
    [SerializeField] private AudioClip spellKnockbackSound;

    [Header("Cooking Sounds")]
    [Tooltip("Sound played when an ingredient is placed into cookware")]
    [SerializeField] private AudioClip kitchenPlaceSound;
    [Tooltip("Sound played when food is sizzling")]
    [SerializeField] private AudioClip cookingSizzleSound;
    [Tooltip("Sound played when food is ready")]
    [SerializeField] private AudioClip cookingReadySound;
    [Tooltip("Sound played when food is burnt")]
    [SerializeField] private AudioClip cookingBurntSound;

    [Header("Other Sounds")]
    [Tooltip("Sound played when entering a portal")]
    [SerializeField] private AudioClip portalEnterSound;
    [Tooltip("Sound played when picking up an item")]
    [SerializeField] private AudioClip pickupSound;
    [Tooltip("Sound played when reloading")]
    [SerializeField] private AudioClip reloadSound;
    [Tooltip("Sound played when shooting")]
    [SerializeField] private AudioClip shootSound;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    public float SfxVolume
    {
        get => sfxVolume;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(sfxVolume, clamped))
                return;

            sfxVolume = clamped;
            ApplySfxVolume();
            PlayerPrefs.SetFloat(SFX_VOLUME_PREF_KEY, sfxVolume);
            OnSfxVolumeChanged?.Invoke(sfxVolume);
        }
    }

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.ignoreListenerVolume = true;
        audioSource.spatialBlend = 0f; // Global 2D sound

        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_PREF_KEY, 1f);
        ApplySfxVolume();
        OnSfxVolumeChanged?.Invoke(sfxVolume);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Optional: Inject sounds into UI buttons
    }

    #region Player Sounds
    public void PlayPlayerDamagedSound() => PlayOneShot(playerDamagedSound);
    public void PlayPlayerDefeatedSound() => PlayOneShot(playerDefeatedSound);
    public void PlayFootstepSound() => PlayOneShot(footstepSound);
    public void PlayJumpSound() => PlayOneShot(jumpSound);
    #endregion

    #region Enemy Sounds
    public void PlayEnemyAttackSound() => PlayOneShot(enemyAttackSound);
    public void PlayEnemyDamagedSound() => PlayOneShot(enemyDamagedSound);
    public void PlayEnemyDefeatedSound() => PlayOneShot(enemyDefeatedSound);
    #endregion

    #region UI Sounds
    public void PlayUIHoverSound() => PlayOneShot(uiHoverSound);
    public void PlayUIClickSound() => PlayOneShot(uiClickSound);
    public void PlayUIOpenSound() => PlayOneShot(uiOpenSound);
    public void PlayUICloseSound() => PlayOneShot(uiCloseSound);
    public void PlayUIDragStartSound() => PlayOneShot(uiDragStartSound);
    public void PlayUIDropSound() => PlayOneShot(uiDropSound);
    public void PlayUISwitchSound() => PlayOneShot(uiSwitchSound);
    #endregion

    #region Wave Sounds
    public void PlayWaveStartSound() => PlayOneShot(waveStartSound);
    public void PlayWaveTickSound() => PlayOneShot(waveTickSound);
    public void PlayWaveEndSound() => PlayOneShot(waveEndSound);
    public void PlayWaveFinalSound() => PlayOneShot(waveFinalSound);
    #endregion

    #region Spell Sounds
    public void PlaySpellSpeedSound() => PlayOneShot(spellSpeedSound);
    public void PlaySpellSpeedEndSound() => PlayOneShot(spellSpeedEndSound);
    public void PlaySpellHealSound() => PlayOneShot(spellHealSound);
    public void PlaySpellProjectileSound() => PlayOneShot(spellProjectileSound);
    public void PlaySpellProjectileImpactSound() => PlayOneShot(spellProjectileImpactSound);
    public void PlaySpellStunSound() => PlayOneShot(spellStunSound);
    public void PlaySpellAoEDamageSound() => PlayOneShot(spellAoEDamageSound);
    public void PlaySpellKnockbackSound() => PlayOneShot(spellKnockbackSound);
    #endregion

    #region Cooking Sounds
    public void PlayKitchenPlaceSound() => PlayOneShot(kitchenPlaceSound);
    public void PlayCookingSizzleSound() => PlayOneShot(cookingSizzleSound);
    public void PlayCookingReadySound() => PlayOneShot(cookingReadySound);
    public void PlayCookingBurntSound() => PlayOneShot(cookingBurntSound);
    #endregion

    #region Other Sounds
    public void PlayPortalEnterSound() => PlayOneShot(portalEnterSound);
    public void PlayPickupSound() => PlayOneShot(pickupSound);
    public void PlayReloadSound() => PlayOneShot(reloadSound);
    public void PlayShootSound() => PlayOneShot(shootSound);
    #endregion

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // For sounds that need volume control
    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void ApplySfxVolume()
    {
        if (audioSource != null)
            audioSource.volume = sfxVolume;
    }
}