using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the OvenDoor GameObject (the one with the door collider).
///
/// Click or drag the door to open/close it.
/// When the door opens the kitchen camera smoothly lerps to an "oven view"
/// Transform you place in the scene; when it closes it lerps back to wherever
/// the camera was before the door was opened.
///
/// Inspector setup
/// ───────────────
///   Kitchen Camera      – the Camera that has KitchenCameraController on it
///   Oven Slot           – the OvenSlot on the oven tray
///   Oven Camera Target  – an empty GameObject you position/rotate in the scene
///                         to define the oven view (low angle, looking into the oven)
///   Lerp Speed          – how fast the camera moves (default 4)
///   Allow Drag          – uncheck for click-only mode
///   Drag Threshold      – pixels of vertical drag needed to open/close
/// </summary>
public class OvenDoorController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The kitchen camera (must also have KitchenCameraController on it).")]
    [SerializeField] private Camera kitchenCamera;

    [Tooltip("The OvenSlot this door belongs to.")]
    [SerializeField] private OvenSlot ovenSlot;

    [Header("Camera")]
    [Tooltip("Place this empty GameObject in the scene where you want the camera " +
             "to sit when the oven door is open (low, angled down into the oven).")]
    [SerializeField] private Transform ovenCameraTarget;

    [Tooltip("How fast the camera lerps to/from the oven view.")]
    [SerializeField] private float lerpSpeed = 4f;

    [Header("Interaction")]
    [Tooltip("Allow dragging the door open/closed in addition to clicking.")]
    [SerializeField] private bool allowDrag = true;

    [Tooltip("Pixels of vertical mouse movement needed to trigger a drag open/close.")]
    [SerializeField] private float dragThreshold = 20f;

    // ── Door state ───────────────────────────────────────────────────────────
    private bool _isOpen;
    private bool _isDragging;
    private bool _dragConsumed;
    private Vector2 _pressStartPos;

    // ── Camera lerp state ────────────────────────────────────────────────────
    private bool _lerpingToOven;       // true  = moving toward oven view
    private bool _lerpingToDefault;    // true  = moving back to saved view
    private float _lerpT;

    // Saved camera pose at the moment the door was opened
    private Vector3 _savedCamPos;
    private Quaternion _savedCamRot;

    // Cached reference to KitchenCameraController so we can disable its input
    // while the camera is locked to the oven view.
    private KitchenCameraController _camController;

    private Mouse _mouse;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _mouse = Mouse.current;

        if (kitchenCamera != null)
            _camController = kitchenCamera.GetComponent<KitchenCameraController>();
    }

    private void Update()
    {
        if (_mouse == null) return;

        if (!SwitchCamera.IsKitchenInteractionAllowed())
        {
            _isDragging = false;
            _dragConsumed = false;
            HandleCameraLerp();
            return;
        }

        HandleInput();
        HandleCameraLerp();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Input
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleInput()
    {
        // Press – hit test against this door's collider
        if (_mouse.leftButton.wasPressedThisFrame)
        {
            Ray ray = kitchenCamera.ScreenPointToRay(_mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            {
                _isDragging = true;
                _dragConsumed = false;
                _pressStartPos = _mouse.position.ReadValue();
            }
        }

        // Drag threshold check
        if (_isDragging && allowDrag && !_dragConsumed)
        {
            Vector2 delta = _mouse.position.ReadValue() - _pressStartPos;

            if (!_isOpen && delta.y < -dragThreshold)
            {
                Open();
                _dragConsumed = true;
            }
            else if (_isOpen && delta.y > dragThreshold)
            {
                Close();
                _dragConsumed = true;
            }
        }

        // Release – if no drag happened, treat as a click toggle
        if (_mouse.leftButton.wasReleasedThisFrame && _isDragging)
        {
            if (!_dragConsumed)
                Toggle();

            _isDragging = false;
            _dragConsumed = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Camera lerp
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleCameraLerp()
    {
        if (kitchenCamera == null || ovenCameraTarget == null) return;
        if (!_lerpingToOven && !_lerpingToDefault) return;

        _lerpT += Time.deltaTime * lerpSpeed;
        float t = Mathf.Clamp01(_lerpT);
        // Smooth the lerp curve so it eases in and out
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        if (_lerpingToOven)
        {
            kitchenCamera.transform.position = Vector3.Lerp(_savedCamPos, ovenCameraTarget.position, smoothT);
            kitchenCamera.transform.rotation = Quaternion.Slerp(_savedCamRot, ovenCameraTarget.rotation, smoothT);
        }
        else // lerpingToDefault
        {
            kitchenCamera.transform.position = Vector3.Lerp(ovenCameraTarget.position, _savedCamPos, smoothT);
            kitchenCamera.transform.rotation = Quaternion.Slerp(ovenCameraTarget.rotation, _savedCamRot, smoothT);
        }

        // Finished
        if (t >= 1f)
        {
            _lerpingToOven = false;
            _lerpingToDefault = false;

            // Re-enable camera controller movement once we've returned to default
            if (!_isOpen && _camController != null)
                _camController.enabled = true;
        }
    }

    private void StartLerpToOven()
    {
        if (kitchenCamera == null || ovenCameraTarget == null) return;

        // Save current camera pose so we can return to it later
        _savedCamPos = kitchenCamera.transform.position;
        _savedCamRot = kitchenCamera.transform.rotation;

        // Disable free camera movement while oven is open
        if (_camController != null)
            _camController.enabled = false;

        _lerpT = 0f;
        _lerpingToOven = true;
        _lerpingToDefault = false;
    }

    private void StartLerpToDefault()
    {
        if (kitchenCamera == null || ovenCameraTarget == null) return;

        _lerpT = 0f;
        _lerpingToDefault = true;
        _lerpingToOven = false;
        // Camera controller re-enabled at the end of the lerp in HandleCameraLerp
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Door control
    // ─────────────────────────────────────────────────────────────────────────

    private void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    private void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        ovenSlot?.NotifyDragInSnapRange();
        StartLerpToOven();
    }

    private void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        ovenSlot?.NotifyDragOutOfSnapRange();
        StartLerpToDefault();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsOpen => _isOpen;
    public void ForceOpen() => Open();
    public void ForceClose() => Close();
}