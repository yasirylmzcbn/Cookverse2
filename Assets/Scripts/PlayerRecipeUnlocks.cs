using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRecipeUnlocks : MonoBehaviour
{
    public static PlayerRecipeUnlocks Instance { get; private set; }

    private static bool _bootstrapPending;
    private static List<Recipe> _bootstrapUnlockedOnStart;
    private static bool _bootstrapPersistToPlayerPrefs;
    private static string _bootstrapPlayerPrefsKey;
    private static bool _bootstrapResetUnlocksOnSessionStart;

    [Header("Starting unlocks")]
    [SerializeField] private List<Recipe> unlockedOnStart = new List<Recipe>();

    [Header("Persistence")]
    [SerializeField] private bool persistToPlayerPrefs = false;
    [SerializeField] private string playerPrefsKey = "cookverse.unlockedRecipes";

    [Header("Audio")]
    [SerializeField] private AudioClip recipeUnlockedSfx;
    [SerializeField, Range(0f, 1f)] private float recipeUnlockedSfxVolume = 1f;

    [Header("Player Attachment")]
    [Tooltip("If enabled and this component is attached to the Player, it will move unlock state to a dedicated persistent object and remove itself from the Player.\n\nDisable this if you want the entire Player to persist between scenes.")]
    [SerializeField] private bool detachFromPlayerOnAwake = false;

    [Tooltip("If enabled, clears any previously saved unlocks when this play session starts (when you press Play).\n\nUnlocks will still persist across scene loads during the current session.")]
    [SerializeField] private bool resetUnlocksOnSessionStart = false;

    private readonly HashSet<Recipe> _unlocked = new HashSet<Recipe>();
    private AudioSource _audioSource;

    public event Action<Recipe> RecipeUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple PlayerRecipeUnlocks found. Instance will remain as '{Instance.gameObject.name}', ignoring '{gameObject.name}'.");
            Destroy(this);
            return;
        }

        // If this component is attached to the Player GameObject, persisting it would also persist the whole player,
        // causing duplicate players across scene loads (and double-input). Instead, bootstrap a dedicated persistent
        // unlocks object and remove this component from the player.
        bool attachedToPlayer = GetComponent<PlayerController>() != null || GetComponentInParent<PlayerController>() != null;
        if (detachFromPlayerOnAwake && attachedToPlayer && !_bootstrapPending)
        {
            _bootstrapPending = true;
            _bootstrapUnlockedOnStart = new List<Recipe>(unlockedOnStart);
            _bootstrapPersistToPlayerPrefs = persistToPlayerPrefs;
            _bootstrapPlayerPrefsKey = playerPrefsKey;
            _bootstrapResetUnlocksOnSessionStart = resetUnlocksOnSessionStart;

            GameObject go = new GameObject("PlayerRecipeUnlocks");
            go.AddComponent<PlayerRecipeUnlocks>();
            DontDestroyOnLoad(go);

            Destroy(this);
            return;
        }

        if (_bootstrapPending)
        {
            // Apply inspector values captured from the player-attached bootstrap.
            unlockedOnStart = _bootstrapUnlockedOnStart ?? new List<Recipe>();
            persistToPlayerPrefs = _bootstrapPersistToPlayerPrefs;
            if (!string.IsNullOrWhiteSpace(_bootstrapPlayerPrefsKey))
                playerPrefsKey = _bootstrapPlayerPrefsKey;
            resetUnlocksOnSessionStart = _bootstrapResetUnlocksOnSessionStart;

            _bootstrapPending = false;
            _bootstrapUnlockedOnStart = null;
            _bootstrapPlayerPrefsKey = null;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();

        _unlocked.Clear();

        if (resetUnlocksOnSessionStart && persistToPlayerPrefs)
            PlayerPrefs.DeleteKey(playerPrefsKey);

        if (persistToPlayerPrefs && !resetUnlocksOnSessionStart)
            LoadFromPrefs();

        for (int i = 0; i < unlockedOnStart.Count; i++)
            _unlocked.Add(unlockedOnStart[i]);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsUnlocked(Recipe recipe) => _unlocked.Contains(recipe);

    public List<Recipe> GetUnlockedRecipes()
    {
        return new List<Recipe>(_unlocked);
    }

    public bool Unlock(Recipe recipe)
    {
        Debug.Log("unlocked recipes before unlock: " + string.Join(", ", _unlocked));
        if (!_unlocked.Add(recipe))
            return false;
        Debug.Log("unlocked recipes after unlock: " + string.Join(", ", _unlocked));
        if (persistToPlayerPrefs)
            SaveToPrefs();

        PlayRecipeUnlockedSfx();

        RecipeUnlocked?.Invoke(recipe);
        return true;
    }

    public void ClearAllUnlocks()
    {
        _unlocked.Clear();
        if (persistToPlayerPrefs)
            PlayerPrefs.DeleteKey(playerPrefsKey);
    }

    public void SetUnlockedRecipes(IEnumerable<Recipe> recipes, bool saveToPrefs = true)
    {
        _unlocked.Clear();
        if (recipes != null)
        {
            foreach (Recipe recipe in recipes)
                _unlocked.Add(recipe);
        }

        if (persistToPlayerPrefs && saveToPrefs)
            SaveToPrefs();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void UnlockAllRecipesForTesting(bool saveToPrefs = false)
    {
        List<Recipe> allRecipes = new List<Recipe>();

        foreach (Recipe recipe in Recipes.RecipeIngredients.Keys)
            allRecipes.Add(recipe);

        SetUnlockedRecipes(allRecipes, saveToPrefs);
    }
#endif

    private void SaveToPrefs()
    {
        // Store as comma-separated ints.
        List<int> values = new List<int>(_unlocked.Count);
        foreach (Recipe r in _unlocked)
            values.Add((int)r);

        string serialized = string.Join(",", values);
        PlayerPrefs.SetString(playerPrefsKey, serialized);
        PlayerPrefs.Save();
    }

    private void LoadFromPrefs()
    {
        if (!PlayerPrefs.HasKey(playerPrefsKey))
            return;

        string serialized = PlayerPrefs.GetString(playerPrefsKey, "");
        if (string.IsNullOrWhiteSpace(serialized))
            return;

        string[] parts = serialized.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int value))
                continue;

            if (!Enum.IsDefined(typeof(Recipe), value))
                continue;

            _unlocked.Add((Recipe)value);
        }
    }

    private void EnsureAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }

    private void PlayRecipeUnlockedSfx()
    {
        if (recipeUnlockedSfx == null)
            return;

        if (_audioSource == null)
            EnsureAudioSource();

        _audioSource.PlayOneShot(recipeUnlockedSfx, recipeUnlockedSfxVolume);
    }
}
