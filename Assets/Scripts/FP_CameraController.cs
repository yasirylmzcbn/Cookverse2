using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float sensX = 28f;
    public float sensY = 26f;

    [Header("Smoothing")]
    [SerializeField, Range(0f, 1f)] private float smoothing = 0.1f; // 0 = no smoothing, higher = more lag

    [Header("Input (New Input System)")]
    [SerializeField] private InputAction lookAction;
    [SerializeField] private InputAction escapeAction;

    private float xRotation = 0f;
    private float targetXRotation = 0f;
    private float targetYRotation = 0f;
    private float currentYRotation = 0f;

    private Vector2 smoothedInput = Vector2.zero;
    private Vector2 inputVelocity = Vector2.zero; // used for SmoothDamp

    [Header("UI")]
    [SerializeField] private SpellMenuUI spellMenu;

    private void Awake()
    {
        EnsureActionsConfigured();
        if (spellMenu == null)
            spellMenu = FindFirstObjectByType<SpellMenuUI>();
    }

    private void OnEnable()
    {
        lookAction?.Enable();
        escapeAction?.Enable();

        // Reset smoothing states to prevent NaN delta corruption on re-enable
        smoothedInput = Vector2.zero;
        inputVelocity = Vector2.zero;
    }

    private void OnDisable()
    {
        // DO NOT disable lookAction here - Unity Input System bug with Mouse Delta freezing permanently
        // lookAction?.Disable();
        escapeAction?.Disable();
    }

    private void EnsureActionsConfigured()
    {
        if (lookAction == null || lookAction.bindings.Count == 0)
        {
            lookAction = new InputAction("Look", InputActionType.Value);
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");
        }
        if (escapeAction == null || escapeAction.bindings.Count == 0)
        {
            escapeAction = new InputAction("Escape", InputActionType.Button);
            escapeAction.AddBinding("<Keyboard>/escape");
            escapeAction.AddBinding("<Gamepad>/start");
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Sync starting Y rotation so we don't snap on first frame
        Transform playerBody = PlayerController.Instance?.PlayerBodyTransform;
        if (playerBody != null)
            targetYRotation = currentYRotation = playerBody.eulerAngles.y;
    }

    void Update()
    {
        Transform playerBody = PlayerController.Instance != null ? PlayerController.Instance.PlayerBodyTransform : null;
        if (playerBody == null) return;

        bool menuOpen = spellMenu != null && spellMenu.menuOpen;

        if (menuOpen) return;

        if (Time.timeScale == 0f) return;

        // --- Read raw input ---
        Vector2 rawInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        // --- Smooth the input with SmoothDamp ---
        // smoothTime closer to 0 = snappier, closer to 0.1+ = floatier
        float smoothTime = Mathf.Lerp(0.01f, 0.12f, smoothing);
        smoothedInput = Vector2.SmoothDamp(smoothedInput, rawInput, ref inputVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        // --- Apply sensitivity ---
        float mouseX = smoothedInput.x * sensX * 0.01f;
        float mouseY = smoothedInput.y * sensY * 0.01f;

        currentYRotation += mouseX;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
    }
}