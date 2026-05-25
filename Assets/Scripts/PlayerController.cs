using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

interface IInteractable
{
    public bool Interact();
}

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public Transform PlayerBodyTransform => transform;

    [Header("Visuals")]
    [Tooltip("Optional root object containing the visible body/mesh. If left empty, all child renderers under the player are used.")]
    [SerializeField] private GameObject bodyVisualRoot;

    private Renderer[] _bodyRenderers;
    private bool _bodyRenderersCached;

    public CharacterController controller;
    public float speed = 10;
    public float gravity = -9.81f * 2;
    public float jumpHeight = 3f;

    public int maxHealth = 100;
    public int currentHealth;

    [Header("Audio")]
    private float _footstepTimer = 0f;

    [Header("Inventory")]
    [SerializeField] private int inventoryCapacity = 12;
    public int InventoryCapacity => inventoryCapacity;
    [Tooltip("If set, movement will be relative to this transform (usually the active camera). If left empty, uses the active SwitchCamera camera, else Camera.main.")]
    [SerializeField] private Transform movementReference;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    public GameObject pickupTrigger;

    [Header("Input (New Input System)")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction jumpAction;
    [SerializeField] private InputAction shootAction;
    [SerializeField] private InputAction reloadAction;
    [SerializeField] private InputAction inventoryToggleAction;


    [Header("Spell Input (New Input System)")]
    [SerializeField] private InputAction spell1Action;
    [SerializeField] private InputAction spell2Action;
    [SerializeField] private InputAction spell3Action;
    [SerializeField] private InputAction spell4Action;

    [Header("Spells")]
    [Tooltip("4-slot spell loadout. Create spells via Assets > Create > Cookverse > Spells and assign them here.")]
    [SerializeField] private SpellDefinition[] spellLoadout = new SpellDefinition[4];
    public SpellDefinition[] GetLoadout() => spellLoadout;
    [Tooltip("Optional database used to auto-equip unlocked spells after scene loads.")]
    [SerializeField] private RecipeSpellDatabase recipeSpellDatabase;

    [Tooltip("Optional cast origin for spells (e.g., hand or staff tip). If null, uses InteractorSource then transform.")]
    [SerializeField] private Transform spellCastOrigin;

    [Header("UI")]
    [SerializeField] private SpellMenuUI spellMenu;

    [Header("Respawn")]
    [SerializeField] private Transform respawnPoint;
    public float respawnDelay = 5f;
    private bool _isDead = false;
    private Coroutine _respawnCoroutine;
    public bool IsDeadOrDown => _isDead || currentHealth <= 0;

    private readonly float[] _nextSpellTimes = new float[4];

    Vector3 velocity;
    bool isGrounded;

    [Header("Interaction")]
    public Transform InteractorSource;
    public float InteractDistance = 3f;

    bool currentlyInteracting = false;

    SwitchCamera switchCamera;

    private float _moveSpeedMultiplier = 1f;
    private Coroutine _buffCoroutine;
    private bool _buffActive;
    private float _buffPrevMoveMultiplier = 1f;
    private float _buffPrevShootCooldownDuration;
    private bool _buffHadShooter;

    private Potato_Shooter _potatoShooter;
    private float _baseShootCooldownDuration;
    private bool _cachedShooterBase;
    private bool _combatEnabled;
    public bool IsCombatEnabled => _combatEnabled;

    private static readonly Dictionary<string, Vector3> _sceneReturnPositions = new Dictionary<string, Vector3>();
    private static readonly Dictionary<string, Quaternion> _sceneReturnRotations = new Dictionary<string, Quaternion>();

    private PlayerRecipeUnlocks _recipeUnlocks;
    private void ResolveRecipeUnlocksIfNeeded()
    {
        if (_recipeUnlocks != null) return;

        // Prefer same object.
        if (TryGetComponent(out PlayerRecipeUnlocks self))
            _recipeUnlocks = self;

        // Common prefab setups put different scripts on parent/child objects.
        if (_recipeUnlocks == null)
            _recipeUnlocks = GetComponentInParent<PlayerRecipeUnlocks>(true);

        if (_recipeUnlocks == null)
            _recipeUnlocks = GetComponentInChildren<PlayerRecipeUnlocks>(true);

        // Fallbacks.
        if (_recipeUnlocks == null)
            _recipeUnlocks = PlayerRecipeUnlocks.Instance;

        if (_recipeUnlocks == null)
            _recipeUnlocks = FindFirstObjectByType<PlayerRecipeUnlocks>();
    }

    public bool IsSpellOnCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _nextSpellTimes.Length) return false;
        return Time.time < _nextSpellTimes[slotIndex];
    }

    public SpellDefinition GetSpell(int slotIndex)
    {
        if (spellLoadout == null) return null;
        if (slotIndex < 0 || slotIndex >= spellLoadout.Length) return null;
        return spellLoadout[slotIndex];
    }

    public bool IsRecipeUnlocked(Recipe recipe)
    {
        ResolveRecipeUnlocksIfNeeded();
        return _recipeUnlocks != null && _recipeUnlocks.IsUnlocked(recipe);
    }

    public bool IsSpellUnlocked(int slotIndex)
    {
        SpellDefinition spell = GetSpell(slotIndex);
        if (spell == null) return false;
        return IsRecipeUnlocked(spell.requiredRecipe);
    }

    public bool TryEquipSpell(SpellDefinition spell, int slotIndex = -1)
    {
        if (spell == null) return false;
        if (!IsRecipeUnlocked(spell.requiredRecipe))
            return false;
        if (spellLoadout == null || spellLoadout.Length != 4)
            spellLoadout = new SpellDefinition[4];

        if (slotIndex >= 0 && slotIndex < spellLoadout.Length)
        {
            spellLoadout[slotIndex] = spell;
            return true;
        }

        for (int i = 0; i < spellLoadout.Length; i++)
        {
            if (spellLoadout[i] == null)
            {
                spellLoadout[i] = spell;
                return true;
            }
        }

        return false;
    }

    public void UnequipSpell(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < spellLoadout.Length)
            spellLoadout[slotIndex] = null;
    }

    public void SetBodyVisible(bool visible)
    {
        CacheBodyRenderersIfNeeded();

        if (_bodyRenderers == null)
            return;

        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            Renderer bodyRenderer = _bodyRenderers[i];
            if (bodyRenderer != null)
                bodyRenderer.enabled = visible;
        }
    }

    void Start()
    {
        switchCamera = FindFirstObjectByType<SwitchCamera>();
        currentHealth = maxHealth;
        DisableCombat();

        ResolveRecipeUnlocksIfNeeded();

    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;

        if (IsDeadOrDown)
            return;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);

        // Play damaged sound via SoundManager
        if (SoundManager.Instance != null && currentHealth < previousHealth)
            SoundManager.Instance.PlayPlayerDamagedSound();

        if (previousHealth > 0 && currentHealth <= 0 && !_isDead)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayPlayerDefeatedSound();

            _isDead = true;
            _respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
    }

    private IEnumerator RespawnRoutine()
    {
        controller.enabled = false;
        // death animation / effect would go here

        yield return new WaitForSeconds(respawnDelay);

        if (respawnPoint != null)
            transform.position = respawnPoint.position;

        currentHealth = maxHealth;

        // reset spell cooldowns and buffs
        for (int i = 0; i < _nextSpellTimes.Length; ++i)
            _nextSpellTimes[i] = 0f;
        ClearActiveBuff(stopCoroutine: true);

        _potatoShooter?.ResetAmmo();
        _isDead = false;

        controller.enabled = true;
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        Debug.Log(currentHealth + "/" + maxHealth);
    }

    public void ApplyTimedBuff(float moveSpeedMultiplier, float shootCooldownMultiplier, float durationSeconds)
    {
        ClearActiveBuff(stopCoroutine: true);
        _buffCoroutine = StartCoroutine(BuffRoutine(moveSpeedMultiplier, shootCooldownMultiplier, durationSeconds));
    }

    private Transform GetMovementReference()
    {
        if (movementReference != null)
            return movementReference;

        if (switchCamera != null)
        {
            if (switchCamera.kitchenCamera != null && switchCamera.kitchenCamera.activeInHierarchy)
                return switchCamera.kitchenCamera.transform;

            if (switchCamera.thirdPersonCamera != null && switchCamera.thirdPersonCamera.activeInHierarchy)
                return switchCamera.thirdPersonCamera.transform;

            if (switchCamera.firstPersonCamera != null && switchCamera.firstPersonCamera.activeInHierarchy)
                return switchCamera.firstPersonCamera.transform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate PlayerController detected during scene load. Keeping existing instance '{Instance.gameObject.name}' and destroying new instance '{gameObject.name}'.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureActionsConfigured();
        _potatoShooter = GetComponentInChildren<Potato_Shooter>(true);
        CacheShooterBaseIfNeeded();
        ResolveRecipeUnlocksIfNeeded();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void CopyRuntimeStateFrom(PlayerController previous)
    {
        if (previous == null) return;

        maxHealth = previous.maxHealth;
        currentHealth = previous.currentHealth;
        speed = previous.speed;
        jumpHeight = previous.jumpHeight;
        gravity = previous.gravity;

        if (previous.spellLoadout != null)
        {
            if (spellLoadout == null || spellLoadout.Length != previous.spellLoadout.Length)
                spellLoadout = new SpellDefinition[previous.spellLoadout.Length];

            for (int i = 0; i < previous.spellLoadout.Length; i++)
                spellLoadout[i] = previous.spellLoadout[i];
        }

        for (int i = 0; i < _nextSpellTimes.Length && i < previous._nextSpellTimes.Length; i++)
            _nextSpellTimes[i] = previous._nextSpellTimes[i];

        _moveSpeedMultiplier = previous._moveSpeedMultiplier;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Scene cameras and other refs live per-scene.
        currentlyInteracting = false;
        switchCamera = FindFirstObjectByType<SwitchCamera>();
        spellMenu = FindFirstObjectByType<SpellMenuUI>();
        ResolveRecipeUnlocksIfNeeded();
        _potatoShooter = GetComponentInChildren<Potato_Shooter>(true);
        CacheShooterBaseIfNeeded();

        RestorePositionForScene(scene.name);
    }

    private void CacheBodyRenderersIfNeeded()
    {
        if (_bodyRenderersCached)
            return;

        if (bodyVisualRoot != null)
            _bodyRenderers = bodyVisualRoot.GetComponentsInChildren<Renderer>(true);
        else
            _bodyRenderers = GetComponentsInChildren<Renderer>(true);

        _bodyRenderersCached = true;
    }

    public static void RememberPositionForScene(string sceneName, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        _sceneReturnPositions[sceneName] = position;
        _sceneReturnRotations[sceneName] = rotation;
    }

    public static void ClearRememberedScenePositions()
    {
        _sceneReturnPositions.Clear();
        _sceneReturnRotations.Clear();
    }

    private void RestorePositionForScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (!_sceneReturnPositions.TryGetValue(sceneName, out Vector3 targetPosition))
            return;

        Quaternion targetRotation = _sceneReturnRotations.TryGetValue(sceneName, out Quaternion savedRotation)
            ? savedRotation
            : transform.rotation;

        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        transform.SetPositionAndRotation(targetPosition, targetRotation);

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();

        if (_combatEnabled)
        {
            shootAction?.Enable();
            reloadAction?.Enable();
            inventoryToggleAction?.Disable();

            spell1Action?.Enable();
            spell2Action?.Enable();
            spell3Action?.Enable();
            spell4Action?.Enable();
        }
        else
        {
            shootAction?.Disable();
            reloadAction?.Disable();
            inventoryToggleAction?.Enable();

            spell1Action?.Disable();
            spell2Action?.Disable();
            spell3Action?.Disable();
            spell4Action?.Disable();
        }
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
        shootAction?.Disable();
        reloadAction?.Disable();
        inventoryToggleAction?.Disable();

        spell1Action?.Disable();
        spell2Action?.Disable();
        spell3Action?.Disable();
        spell4Action?.Disable();
    }

    public void DisableCombat()
    {
        _combatEnabled = false;
        shootAction?.Disable();
        reloadAction?.Disable();
        _potatoShooter?.gameObject.SetActive(false);
        inventoryToggleAction?.Enable();

        spell1Action?.Disable();
        spell2Action?.Disable();
        spell3Action?.Disable();
        spell4Action?.Disable();
    }

    public void EnableCombat()
    {
        _combatEnabled = true;
        shootAction?.Enable();
        reloadAction?.Enable();
        _potatoShooter?.gameObject.SetActive(true);
        inventoryToggleAction?.Disable();

        spell1Action?.Enable();
        spell2Action?.Enable();
        spell3Action?.Enable();
        spell4Action?.Enable();
    }

    private void EnsureActionsConfigured()
    {
        if (moveAction == null || moveAction.bindings.Count == 0)
        {
            moveAction = new InputAction("Move", InputActionType.Value);

            // Keyboard WASD / arrows
            var composite = moveAction.AddCompositeBinding("2DVector");
            composite.With("Up", "<Keyboard>/w");
            composite.With("Down", "<Keyboard>/s");
            composite.With("Left", "<Keyboard>/a");
            composite.With("Right", "<Keyboard>/d");

            composite = moveAction.AddCompositeBinding("2DVector");
            composite.With("Up", "<Keyboard>/upArrow");
            composite.With("Down", "<Keyboard>/downArrow");
            composite.With("Left", "<Keyboard>/leftArrow");
            composite.With("Right", "<Keyboard>/rightArrow");

            moveAction.AddBinding("<Gamepad>/leftStick");
        }

        if (jumpAction == null || jumpAction.bindings.Count == 0)
        {
            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        if (shootAction == null || shootAction.bindings.Count == 0)
        {
            shootAction = new InputAction("Shoot", InputActionType.Button);
            shootAction.AddBinding("<Mouse>/leftButton");
            shootAction.AddBinding("<Gamepad>/rightTrigger");
        }
        if (reloadAction == null || reloadAction.bindings.Count == 0)
        {
            reloadAction = new InputAction("Reload", InputActionType.Button);
            reloadAction.AddBinding("<Keyboard>/r");
            reloadAction.AddBinding("<Gamepad>/buttonWest");
        }
        if (inventoryToggleAction == null || inventoryToggleAction.bindings.Count == 0)
        {
            inventoryToggleAction = new InputAction("InventoryToggle", InputActionType.Button);
            inventoryToggleAction.AddBinding("<Keyboard>/i");
            inventoryToggleAction.AddBinding("<Gamepad>/buttonSelect");
        }

        // Spells (4 slots). Defaults support both keyboard and gamepad at the same time.
        if (spell1Action == null || spell1Action.bindings.Count == 0)
        {
            spell1Action = new InputAction("Spell1", InputActionType.Button);
            spell1Action.AddBinding("<Keyboard>/1");
            spell1Action.AddBinding("<Keyboard>/numpad1");
            spell1Action.AddBinding("<Gamepad>/dpad/up");
        }
        if (spell2Action == null || spell2Action.bindings.Count == 0)
        {
            spell2Action = new InputAction("Spell2", InputActionType.Button);
            spell2Action.AddBinding("<Keyboard>/2");
            spell2Action.AddBinding("<Keyboard>/numpad2");
            spell2Action.AddBinding("<Gamepad>/dpad/right");
        }
        if (spell3Action == null || spell3Action.bindings.Count == 0)
        {
            spell3Action = new InputAction("Spell3", InputActionType.Button);
            spell3Action.AddBinding("<Keyboard>/3");
            spell3Action.AddBinding("<Keyboard>/numpad3");
            spell3Action.AddBinding("<Gamepad>/dpad/down");
        }
        if (spell4Action == null || spell4Action.bindings.Count == 0)
        {
            spell4Action = new InputAction("Spell4", InputActionType.Button);
            spell4Action.AddBinding("<Keyboard>/4");
            spell4Action.AddBinding("<Keyboard>/numpad4");
            spell4Action.AddBinding("<Gamepad>/dpad/left");
        }
    }

    void Update()
    {
        if (_isDead) return;

        if (spellMenu != null && spellMenu.menuOpen)
        {
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            if (inventoryToggleAction != null && inventoryToggleAction.WasPressedThisFrame())
                spellMenu.SetMenuVisible(false);

            return;
        }
        if (inventoryToggleAction != null && inventoryToggleAction.WasPressedThisFrame())
        {
            Debug.Log("Inventory toggle pressed");
            if (spellMenu == null)
                spellMenu = FindFirstObjectByType<SpellMenuUI>(FindObjectsInactive.Include);
            var isOpen = spellMenu != null && spellMenu.menuOpen;
            if (spellMenu != null)
                spellMenu.SetMenuVisible(!isOpen);
        }
        if (currentlyInteracting)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                currentlyInteracting = false;
                Debug.Log("Stopped interacting via input.");
                EnablePickupTrigger(true);
                switchCamera.ExitKitchenCamera();
            }
            return;
        }

        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        float x = moveInput.x;
        float z = moveInput.y;

        Transform reference = GetMovementReference();
        Vector3 referenceForward = reference != null ? reference.forward : transform.forward;
        Vector3 referenceRight = reference != null ? reference.right : transform.right;
        referenceForward.y = 0f;
        referenceRight.y = 0f;
        referenceForward = referenceForward.sqrMagnitude > 0.0001f ? referenceForward.normalized : transform.forward;
        referenceRight = referenceRight.sqrMagnitude > 0.0001f ? referenceRight.normalized : transform.right;

        Vector3 move = (referenceRight * x) + (referenceForward * z);

        move = Vector3.ClampMagnitude(move, 1f);
        Vector3 moveVelocity = move * (speed * _moveSpeedMultiplier);
        controller.Move(moveVelocity * Time.deltaTime);

        if (jumpAction != null && jumpAction.WasPressedThisFrame() && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * 2f * -gravity);
            
            // Play jump sound via SoundManager
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayJumpSound();
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Play footstep sounds while walking (every 0.4 seconds)
        if (isGrounded)
        {
            Vector2 inputMag = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (inputMag.magnitude > 0.1f)
            {
                _footstepTimer += Time.deltaTime;
                if (_footstepTimer >= 0.4f)
                {
                    _footstepTimer = 0f;
                    if (SoundManager.Instance != null)
                        SoundManager.Instance.PlayFootstepSound();
                }
            }
            else
            {
                _footstepTimer = 0f;
            }
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
            if (Physics.Raycast(r, out RaycastHit hit, InteractDistance))
            {
                Debug.Log("Hit " + hit.collider.gameObject.name);
                IInteractable interactable = null;

                // 1) Exact collider object
                hit.collider.gameObject.TryGetComponent(out interactable);

                // 2) Parent chain (common when collider is on a child)
                if (interactable == null)
                    interactable = hit.collider.GetComponentInParent<IInteractable>();

                // 3) Rigidbody root (common for compound colliders)
                if (interactable == null && hit.rigidbody != null)
                    interactable = hit.rigidbody.GetComponentInParent<IInteractable>();

                if (interactable != null)
                {
                    currentlyInteracting = interactable.Interact();
                    // if interacting, disable picking up
                    EnablePickupTrigger(!currentlyInteracting);
                    Debug.Log("Enabled pickup trigger: " + !currentlyInteracting);

                }
                Debug.Log("Interactable: " + interactable);
            }
        }

        if (spell1Action != null && spell1Action.WasPressedThisFrame()) TryCastSpell(0);
        if (spell2Action != null && spell2Action.WasPressedThisFrame()) TryCastSpell(1);
        if (spell3Action != null && spell3Action.WasPressedThisFrame()) TryCastSpell(2);
        if (spell4Action != null && spell4Action.WasPressedThisFrame()) TryCastSpell(3);

        // Add a shoot cooldown later
        if (shootAction != null && shootAction.IsPressed())
        {
            var potatoShooter = GetPotatoShooter();
            if (potatoShooter != null) potatoShooter.Shoot();
        }

        if (reloadAction != null && reloadAction.WasPressedThisFrame())
        {
            Debug.Log("reload pressed");
            var potatoShooter = GetPotatoShooter();
            if (potatoShooter != null) potatoShooter.TryReload();
        }
    }
    private bool IsSpellEquipped(SpellDefinition spell)
    {
        if (spell == null || spellLoadout == null) return false;
        for (int i = 0; i < spellLoadout.Length; i++)
        {
            if (spellLoadout[i] == spell)
                return true;
        }

        return false;
    }

    private void TryCastSpell(int slotIndex)
    {
        SpellDefinition spell = GetSpell(slotIndex);
        if (spell == null)
        {
            Debug.LogWarning($"No spell equipped in slot {slotIndex}.");
            return;
        }

        bool unlocked = IsSpellUnlocked(slotIndex);
        if (!unlocked)
        {
            Debug.LogWarning($"Spell '{spell.displayName}' in slot {slotIndex} is locked. RequiredRecipe={spell.requiredRecipe}, HasUnlock={IsRecipeUnlocked(spell.requiredRecipe)}");
            return;
        }
        Debug.LogWarning($"{spell.displayName} is unlocked on slot " + slotIndex);
        if (IsSpellOnCooldown(slotIndex)) return;

        Transform origin = spellCastOrigin != null
            ? spellCastOrigin
            : (InteractorSource != null ? InteractorSource : transform);

        Vector3 forward = origin != null ? origin.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        forward.Normalize();

        var context = new SpellCastContext(this, origin, forward);
        if (!spell.CanCast(in context)) return;

        PlaySpellCastSound(spell);
        spell.Cast(in context);
        float cd = Mathf.Max(0f, spell.cooldownSeconds);
        _nextSpellTimes[slotIndex] = Time.time + cd;
    }

    private void PlaySpellCastSound(SpellDefinition spell)
    {
        if (SoundManager.Instance == null || spell == null)
            return;

        switch (spell)
        {
            case ProjectileSpell:
                SoundManager.Instance.PlaySpellProjectileSound();
                break;
            case HealSpell:
                SoundManager.Instance.PlaySpellHealSound();
                break;
            case TimedBuffSpell:
                SoundManager.Instance.PlaySpellSpeedSound();
                break;
            case AoEStunSpell:
                SoundManager.Instance.PlaySpellStunSound();
                break;
            case AoEDamageSpell:
                SoundManager.Instance.PlaySpellAoEDamageSound();
                break;
            case AoEKnockbackSpell:
                SoundManager.Instance.PlaySpellKnockbackSound();
                break;
        }
    }

    public void EnablePickupTrigger(bool enable)
    {
        if (pickupTrigger != null)
            pickupTrigger.SetActive(enable);
    }

    private Potato_Shooter GetPotatoShooter()
    {
        if (_potatoShooter == null)
            _potatoShooter = GetComponentInChildren<Potato_Shooter>(true);
        CacheShooterBaseIfNeeded();
        return _potatoShooter;
    }

    private void CacheShooterBaseIfNeeded()
    {
        if (_cachedShooterBase) return;
        if (_potatoShooter == null) return;
        _baseShootCooldownDuration = _potatoShooter.shootCooldownDuration;
        _cachedShooterBase = true;
    }

    private IEnumerator BuffRoutine(float moveSpeedMultiplier, float shootCooldownMultiplier, float durationSeconds)
    {
        moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
        shootCooldownMultiplier = Mathf.Max(0f, shootCooldownMultiplier);

        _buffPrevMoveMultiplier = _moveSpeedMultiplier;
        _moveSpeedMultiplier = _buffPrevMoveMultiplier * moveSpeedMultiplier;

        var shooter = GetPotatoShooter();
        _buffHadShooter = shooter != null;
        if (_buffHadShooter)
        {
            CacheShooterBaseIfNeeded();
            _buffPrevShootCooldownDuration = shooter.shootCooldownDuration;
            shooter.shootCooldownDuration = _baseShootCooldownDuration * shootCooldownMultiplier;
        }

        _buffActive = true;

        yield return new WaitForSeconds(durationSeconds);

        ClearActiveBuff(stopCoroutine: false, playEndSound: true);
    }

    private void ClearActiveBuff(bool stopCoroutine, bool playEndSound = false)
    {
        if (stopCoroutine && _buffCoroutine != null)
        {
            StopCoroutine(_buffCoroutine);
            _buffCoroutine = null;
        }

        if (!_buffActive) return;

        _moveSpeedMultiplier = _buffPrevMoveMultiplier;

        if (_buffHadShooter)
        {
            var shooter = GetPotatoShooter();
            if (shooter != null)
                shooter.shootCooldownDuration = _buffPrevShootCooldownDuration;
        }

        if (playEndSound && SoundManager.Instance != null)
            SoundManager.Instance.PlaySpellSpeedEndSound();

        _buffActive = false;
        _buffHadShooter = false;
        _buffPrevMoveMultiplier = 1f;
        _buffPrevShootCooldownDuration = 0f;
        _buffCoroutine = null;
    }

    public void ResetCombatState()
    {
        for (int i = 0; i < _nextSpellTimes.Length; ++i)
            _nextSpellTimes[i] = 0f;
        ClearActiveBuff(stopCoroutine: true);
        _potatoShooter?.ResetAmmo();
        currentHealth = maxHealth;
    }

    public void EndInteraction()
    {
        currentlyInteracting = false;
        EnablePickupTrigger(true);
    }
}