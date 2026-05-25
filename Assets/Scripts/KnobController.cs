using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;


public class KnobController : MonoBehaviour
{
    public enum BurnerSide { Left, Right }

    public enum RotationAxisMode
    {
        Manual,
        AutoFromOrientation,
        AutoFromMountReference,
    }

    public enum AxisSpace
    {
        Local,
        World,
    }

    public enum LocalAxis
    {
        Right,
        Up,
        Forward,
    }

    [Header("Camera Reference")]
    [SerializeField] public Camera kitchenCamera;

    [Header("Cookware Reference")]
    [Tooltip("Backwards-compatible single cookware reference (stove). Optional if 'cookwareSlots' is set.")]
    [SerializeField] public GameObject cookware;
    [Tooltip("Explicitly chosen cookware type")]
    [SerializeField] private CookwareType cookwareType;
    [Tooltip("One or more cookware slots controlled by this knob (fryer trays, oven racks, etc.).")]
    [SerializeField] protected CookwareSlot[] cookwareSlots;

    [Header("Animator")]
    [Tooltip("Animator that gets triggered when knob is turned on/off")]
    [SerializeField] private Animator animator;

    [Header("Rotation Settings")]
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 180f;

    [Header("Click Toggle")]
    [Tooltip("Angle used when the knob is toggled OFF.")]
    [SerializeField] private float offAngle = 0f;

    [Tooltip("Angle used when the knob is toggled ON.")]
    [SerializeField] private float onAngle = 90f;

    [Tooltip("If enabled, knob rotates smoothly to ON/OFF instead of snapping instantly.")]
    [SerializeField] private bool smoothTurn = true;

    [Tooltip("How long the smooth turn takes in seconds.")]
    [SerializeField, Min(0f)] private float turnDuration = 0.15f;

    [Tooltip("Interpolation curve used for smooth turning.")]
    [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("How the knob decides which axis to rotate around.")]
    [SerializeField] private RotationAxisMode rotationAxisMode = RotationAxisMode.Manual;

    [Tooltip("Whether the axis is interpreted in local space or world space.\n- Local: respects how the knob is placed/rotated.\n- World: uses global X/Y/Z axes.")]
    [SerializeField] private AxisSpace axisSpace = AxisSpace.Local;

    [Tooltip("Manual rotation axis (used when Rotation Axis Mode = Manual). Interpreted in the chosen Axis Space.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("When auto-detecting from orientation, which local axis should be treated as the knob's rotation axis.")]
    [SerializeField] private LocalAxis orientationSourceAxis = LocalAxis.Up;

    [Tooltip("If true and Axis Space = World, the auto axis snaps to the nearest world axis (X/Y/Z).")]
    [SerializeField] private bool snapAutoAxisToWorldCardinal = true;

    [Tooltip("Optional mount reference (e.g., the stove body / cube). If set and Rotation Axis Mode = AutoFromMountReference, the axis is picked from (knobPos - mountPos) and snapped to X/Y/Z.")]
    [SerializeField] private Transform mountReference;

    [Header("State")]
    [SerializeField] public BurnerSide side;

    [Header("Audio")]
    [SerializeField] private AudioClip turnOnSfx;
    [SerializeField] private AudioClip turnOffSfx;
    [SerializeField, Range(0f, 1f)] private float knobSfxVolume = 1f;

    private float currentAngle = 0f;
    private bool isOnState = false;
    private Mouse mouse;
    private StoveScript stove;
    private Coroutine turnCoroutine;

    private Quaternion initialLocalRotation;
    private Quaternion initialWorldRotation;
    private AudioSource _audioSource;

    void Start()
    {
        mouse = Mouse.current;
        stove = GetComponentInParent<StoveScript>();
        EnsureAudioSource();

        initialLocalRotation = transform.localRotation;
        initialWorldRotation = transform.rotation;

        if ((cookwareSlots == null || cookwareSlots.Length == 0) && cookware != null)
        {
            CookwareSlot single = cookware.GetComponent<CookwareSlot>();
            if (single != null)
                cookwareSlots = new[] { single };
        }

        if (cookwareSlots == null || cookwareSlots.Length == 0)
        {
            Debug.LogWarning($"Knob {gameObject.name} has no CookwareSlot targets assigned.");
            return;
        }

        for (int i = 0; i < cookwareSlots.Length; i++)
        {
            if (cookwareSlots[i] == null)
            {
                Debug.LogWarning($"Knob {gameObject.name} has a null CookwareSlot at index {i}.");
                continue;
            }
            cookwareSlots[i].IsOn = false;
        }

        SetState(false, immediate: true);
    }

    void Update()
    {
        if (mouse == null) return;

        if (!SwitchCamera.IsKitchenInteractionAllowed())
        {
            return;
        }

        if (kitchenCamera == null) return;

        // Click this knob to toggle directly between OFF and ON angles.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Ray ray = kitchenCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    ToggleState();
                }
            }
        }
    }

    private void ToggleState()
    {
        bool newIsOn = !isOnState;
        SetState(newIsOn);
    }

    private void SetState(bool isOn, bool immediate = false)
    {
        isOnState = isOn;
        float targetAngle = isOn ? onAngle : offAngle;

        if (turnCoroutine != null)
        {
            StopCoroutine(turnCoroutine);
            turnCoroutine = null;
        }

        float clampedTargetAngle = Mathf.Clamp(targetAngle, minAngle, maxAngle);
        if (immediate || !smoothTurn || turnDuration <= 0f)
        {
            currentAngle = clampedTargetAngle;
            ApplyKnobRotation(currentAngle);
            UpdateHeatVisuals();
        }
        else
        {
            turnCoroutine = StartCoroutine(AnimateToAngle(clampedTargetAngle));
        }

        UpdateCookwareState(isOn);
    }

    private void UpdateCookwareState(bool isOn)
    {
        if (stove != null)
            stove.ApplyVisuals(side, GetHeatLevel());

        bool anyChanged = false;
        if (cookwareSlots != null)
        {
            for (int i = 0; i < cookwareSlots.Length; i++)
            {
                CookwareSlot slot = cookwareSlots[i];
                if (slot == null) continue;

                bool wasOn = slot.IsOn;
                slot.IsOn = isOn;
                if (wasOn != isOn)
                    anyChanged = true;
            }
        }

        if (anyChanged)
            OnStateChanged(isOn);
    }

    private IEnumerator AnimateToAngle(float targetAngle)
    {
        float startAngle = currentAngle;
        float elapsed = 0f;

        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / turnDuration);
            float curvedT = turnCurve != null ? turnCurve.Evaluate(t) : t;

            currentAngle = Mathf.Lerp(startAngle, targetAngle, curvedT);
            ApplyKnobRotation(currentAngle);
            UpdateHeatVisuals();
            yield return null;
        }

        currentAngle = targetAngle;
        ApplyKnobRotation(currentAngle);
        UpdateHeatVisuals();
        turnCoroutine = null;
    }

    private void UpdateHeatVisuals()
    {
        if (stove != null)
            stove.ApplyVisuals(side, GetHeatLevel());
    }

    private void ApplyKnobRotation(float angleDegrees)
    {
        if (axisSpace == AxisSpace.World)
        {
            Vector3 axisWorld = GetRotationAxisWorld();
            transform.rotation = Quaternion.AngleAxis(angleDegrees, axisWorld) * initialWorldRotation;
        }
        else
        {
            Vector3 axisLocal = GetRotationAxisLocal();
            transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(angleDegrees, axisLocal);
        }
    }

    private Vector3 GetRotationAxisLocal()
    {
        if (rotationAxisMode == RotationAxisMode.Manual)
            return SafeNormalized(rotationAxis, Vector3.up);

        // Auto modes in local space: rotate around the chosen local axis so placement/orientation drives behavior.
        return orientationSourceAxis switch
        {
            LocalAxis.Right => Vector3.right,
            LocalAxis.Forward => Vector3.forward,
            _ => Vector3.up,
        };
    }

    private Vector3 GetRotationAxisWorld()
    {
        if (rotationAxisMode == RotationAxisMode.Manual)
            return SafeNormalized(rotationAxis, Vector3.up);

        if (rotationAxisMode == RotationAxisMode.AutoFromMountReference && mountReference != null)
        {
            Vector3 fromMountToKnob = transform.position - mountReference.position;
            Vector3 snapped = SnapToWorldCardinal(fromMountToKnob);
            return SafeNormalized(snapped, Vector3.up);
        }

        // Fallback / AutoFromOrientation
        Vector3 sourceWorld = orientationSourceAxis switch
        {
            LocalAxis.Right => initialWorldRotation * Vector3.right,
            LocalAxis.Forward => initialWorldRotation * Vector3.forward,
            _ => initialWorldRotation * Vector3.up,
        };

        if (snapAutoAxisToWorldCardinal)
            sourceWorld = SnapToWorldCardinal(sourceWorld);

        return SafeNormalized(sourceWorld, Vector3.up);
    }

    private static Vector3 SnapToWorldCardinal(Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-8f)
            return Vector3.up;

        float ax = Mathf.Abs(direction.x);
        float ay = Mathf.Abs(direction.y);
        float az = Mathf.Abs(direction.z);

        if (ax >= ay && ax >= az)
            return Mathf.Sign(direction.x) * Vector3.right;
        if (ay >= ax && ay >= az)
            return Mathf.Sign(direction.y) * Vector3.up;
        return Mathf.Sign(direction.z) * Vector3.forward;
    }

    private static Vector3 SafeNormalized(Vector3 v, Vector3 fallback)
    {
        float mag = v.magnitude;
        return mag > 1e-6f ? (v / mag) : fallback;
    }

    protected virtual void OnStateChanged(bool newState)
    {
        Debug.Log($"Knob {gameObject.name} is now: {(newState ? "ON" : "OFF")}");
        PlayToggleSfx(newState);
        if (cookwareType == CookwareType.Fryer)
        {
            animator.SetBool("isOn", newState);
        }
    }

    // Public method to get current angle (useful for heat levels)
    public float GetCurrentAngle()
    {
        return currentAngle;
    }

    // Get normalized heat level (0 to 1)
    public float GetHeatLevel()
    {
        return Mathf.InverseLerp(minAngle, maxAngle, currentAngle);
    }

    private void EnsureAudioSource()
    {
        if (_audioSource != null)
            return;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }

    private void PlayToggleSfx(bool isOn)
    {
        AudioClip clip = isOn ? turnOnSfx : turnOffSfx;
        if (clip == null)
            return;

        EnsureAudioSource();
        if (_audioSource != null)
            _audioSource.PlayOneShot(clip, knobSfxVolume);
    }
}