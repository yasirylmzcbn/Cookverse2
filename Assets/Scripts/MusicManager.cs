using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("References")]
    [SerializeField] private AudioSource musicSource;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    public float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            if (musicSource != null)
                musicSource.volume = musicVolume;
        }
    }

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Tracks")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip kitchenMusic;
    [SerializeField] private AudioClip combatEasyMusic;
    [SerializeField] private AudioClip combatMediumMusic;
    [SerializeField] private AudioClip combatHardMusic;
    [SerializeField] private AudioClip combatBossMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource == null)
        {
            Debug.LogError("MusicManager requires an AudioSource.");
            enabled = false;
            return;
        }

        musicSource.ignoreListenerVolume = true;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ApplyTargetTrack();
    }

    private void Update()
    {
        // Combat mode and difficulty can change without scene transitions.
        ApplyTargetTrack();
    }

    private void OnValidate()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;

        if (!Application.isPlaying)
            return;

        ApplyTargetTrack();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyTargetTrack(forceRestart: true);
    }

    private void ApplyTargetTrack(bool forceRestart = false)
    {
        if (musicSource == null)
            return;

        AudioClip targetClip = ResolveTargetTrack();
        if (targetClip == null)
        {
            if (musicSource.isPlaying)
                musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        if (!forceRestart && musicSource.clip == targetClip && musicSource.isPlaying)
            return;

        if (musicSource.isPlaying)
            musicSource.Stop();

        musicSource.clip = targetClip;
        musicSource.Play();
    }

    private AudioClip ResolveTargetTrack()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(activeSceneName, mainMenuSceneName, StringComparison.OrdinalIgnoreCase))
            return mainMenuMusic;

        bool isCombatEnabled = PlayerController.Instance != null && PlayerController.Instance.IsCombatEnabled;
        if (!isCombatEnabled)
            return kitchenMusic;

        switch (ResolveCombatDifficulty())
        {
            case GameManager.Difficulty.Medium:
                return combatMediumMusic;
            case GameManager.Difficulty.Hard:
                return combatHardMusic;
            case GameManager.Difficulty.Boss:
                return combatBossMusic;
            case GameManager.Difficulty.None:
            case GameManager.Difficulty.Easy:
            default:
                return combatEasyMusic;
        }
    }

    private GameManager.Difficulty ResolveCombatDifficulty()
    {
        if (GameManager.Instance == null)
            return GameManager.Difficulty.Easy;

        int wave = GameManager.Instance.CurrentWave();
        if (wave >= 30)
            return GameManager.Difficulty.Boss;
        if (wave >= 20)
            return GameManager.Difficulty.Hard;
        if (wave >= 10)
            return GameManager.Difficulty.Medium;

        return GameManager.Difficulty.Easy;
    }
}
