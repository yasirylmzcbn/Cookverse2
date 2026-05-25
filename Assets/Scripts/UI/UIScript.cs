using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.InputSystem;

public class UIScript : MonoBehaviour
{
    [Header("HUD Toggle")]
    [Tooltip("Preferred key for HUD toggle. Will fall back to F1 if this key is already used by player input bindings.")]
    [SerializeField] private KeyCode preferredHudToggleKey = KeyCode.Tab;
    [SerializeField] private KeyCode fallbackHudToggleKey = KeyCode.F1;
    [Tooltip("UI roots to hide/show as HUD. If empty, this GameObject is used.")]
    [SerializeField] private List<GameObject> hudRoots = new List<GameObject>();

    private KeyCode _activeHudToggleKey;
    private bool _hudVisible = true;

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    private PlayerController _playerController;
    private Potato_Shooter _potatoShooter;
    [SerializeField] private QuestManager questManager;
    private QuestManager _questManager;

    [Header("Runtime")]
    [Tooltip("How often to retry finding player/shooter references when missing.")]
    [SerializeField] private float resolveRetryIntervalSeconds = 0.25f;
    private float _nextResolveTime;

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _nextResolveTime = 0f;
        ResolveReferences();
        EnsureHudRootsConfigured();
        SelectHudToggleKey();
        ApplyHudVisibility();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Player may be recreated/changed depending on scene setup.
        _playerController = null;
        _potatoShooter = null;
        _nextResolveTime = 0f;
        ResolveReferences();
        EnsureHudRootsConfigured();
        SelectHudToggleKey();
        ApplyHudVisibility();
    }

    private void EnsureHudRootsConfigured()
    {
        if (hudRoots == null)
            hudRoots = new List<GameObject>();

        if (hudRoots.Count == 0)
            hudRoots.Add(gameObject);
    }

    private void SelectHudToggleKey()
    {
        _activeHudToggleKey = IsKeyUsedByPlayerBindings(preferredHudToggleKey)
            ? fallbackHudToggleKey
            : preferredHudToggleKey;
    }

    private bool IsKeyUsedByPlayerBindings(KeyCode key)
    {
        PlayerController player = PlayerController.Instance != null
            ? PlayerController.Instance
            : FindFirstObjectByType<PlayerController>();

        if (player == null)
            return false;

        string expectedPath = key == KeyCode.Tab ? "<Keyboard>/tab" : key == KeyCode.F1 ? "<Keyboard>/f1" : string.Empty;
        if (string.IsNullOrEmpty(expectedPath))
            return false;

        // Prefer scanning serialized InputAction fields on PlayerController.
        FieldInfo[] fields = typeof(PlayerController).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        foreach (FieldInfo field in fields)
        {
            if (field == null || field.FieldType != typeof(InputAction))
                continue;

            InputAction action = field.GetValue(player) as InputAction;
            if (action == null)
                continue;

            foreach (InputBinding binding in action.bindings)
            {
                if (string.Equals(binding.effectivePath, expectedPath, System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(binding.path, expectedPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Fallback: scan all actions on attached PlayerInput if present.
        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return false;

        foreach (InputAction action in playerInput.actions)
        {
            if (action == null)
                continue;

            foreach (InputBinding binding in action.bindings)
            {
                if (string.Equals(binding.effectivePath, expectedPath, System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(binding.path, expectedPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ApplyHudVisibility()
    {
        if (hudRoots == null)
            return;

        for (int i = 0; i < hudRoots.Count; i++)
        {
            GameObject hudRoot = hudRoots[i];
            if (hudRoot == null)
                continue;

            if (hudRoot == gameObject)
            {
                for (int c = 0; c < transform.childCount; c++)
                {
                    Transform child = transform.GetChild(c);
                    if (child != null)
                        child.gameObject.SetActive(_hudVisible);
                }
                continue;
            }

            hudRoot.SetActive(_hudVisible);
        }
    }

    private void ResolveReferences()
    {
        if (_playerController == null)
        {
            _playerController = playerController != null
                ? playerController
                : (PlayerController.Instance != null ? PlayerController.Instance : FindFirstObjectByType<PlayerController>());

            playerController = _playerController;
        }

        if (_potatoShooter == null && _playerController != null)
            _potatoShooter = _playerController.GetComponentInChildren<Potato_Shooter>(true);

        if (_questManager == null)
        {
            _questManager = questManager != null ? questManager : QuestManager.Instance;
            if (_questManager == null)
                _questManager = FindFirstObjectByType<QuestManager>();

            questManager = _questManager;
        }
    }

    [Header("Health UI")]
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Ammo UI")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Quests UI")]
    [SerializeField] private TextMeshProUGUI questText;

    [Header("UI Transparency")]
    [SerializeField, Range(0f, 1f)] private float dimmedAlpha = 0.3f;

    void Start()
    {
        ResolveReferences();
    }

    void Update()
    {
        if (Input.GetKeyDown(_activeHudToggleKey))
        {
            _hudVisible = !_hudVisible;
            ApplyHudVisibility();
            return;
        }

        if ((_playerController == null || _potatoShooter == null) && Time.unscaledTime >= _nextResolveTime)
        {
            _nextResolveTime = Time.unscaledTime + Mathf.Max(0.05f, resolveRetryIntervalSeconds);
            ResolveReferences();
        }

        if (_playerController != null)
        {
            if (healthBarSlider != null)
            {
                healthBarSlider.maxValue = _playerController.maxHealth;
                healthBarSlider.value = _playerController.currentHealth;
            }

            if (healthText != null)
                healthText.text = _playerController.currentHealth + " / " + _playerController.maxHealth;
        }

        if (_potatoShooter != null && ammoText != null)
        {
            ammoText.text = _potatoShooter.currentAmmo + " / " + _potatoShooter.maxAmmo;

            // more transparent text during reload
            var c = ammoText.color;
            c.a = (_potatoShooter.currentAmmo <= 0 || _potatoShooter.IsReloading) ? dimmedAlpha : 1f;
            ammoText.color = c;
        }

        if (questText != null)
        {
            if (_questManager != null)
                questText.text = _questManager.GetQuestDisplayText();
            else
                questText.text = "> No active quest";

            var qc = questText.color;
            qc.a = (_questManager != null && _questManager.IsCompleted) ? dimmedAlpha : 1f;
            questText.color = qc;
        }
    }
}
