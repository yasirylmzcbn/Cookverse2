using UnityEngine;
using UnityEngine.InputSystem;

public class KitchenCameraController : MonoBehaviour
{
    private enum MoveSpace
    {
        WorldXZ = 0,
        CameraRelative = 1,
        ReferenceTransform = 2,
    }

    private enum ZoomMode
    {
        Auto = 0,
        OrthographicSize = 1,
        Height = 2,
        FieldOfView = 3,
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 10f;
    [Tooltip("How WASD movement should be interpreted. CameraRelative makes W always move toward the top of the screen.")]
    [SerializeField] private MoveSpace moveSpace = MoveSpace.CameraRelative;
    [Tooltip("Used when Move Space is ReferenceTransform.")]
    [SerializeField] private Transform movementReference;
    [Tooltip("If enabled, camera movement is constrained to X/Z bounds.")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Vector2 boundsX = new Vector2(-50f, 50f);
    [SerializeField] private Vector2 boundsZ = new Vector2(-50f, 50f);

    [Header("Zoom")]
    [SerializeField] private ZoomMode zoomMode = ZoomMode.Auto;
    [SerializeField, Min(0f)] private float zoomSpeed = 5f;

    [Tooltip("Min/max height (Y) when Zoom Mode is Height (or Auto with perspective camera).")]
    [SerializeField] private Vector2 heightLimits = new Vector2(5f, 40f);

    [Tooltip("Min/max orthographic size when Zoom Mode is OrthographicSize (or Auto with ortho camera).")]
    [SerializeField] private Vector2 orthoSizeLimits = new Vector2(3f, 25f);

    [Tooltip("Min/max field of view when Zoom Mode is FieldOfView.")]
    [SerializeField] private Vector2 fovLimits = new Vector2(15f, 75f);

    [Header("Input")]
    [Tooltip("If enabled, movement and zoom use unscaled time (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime;

    private InputAction moveAction;
    private InputAction zoomAction;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
    }

    private void OnEnable()
    {
        SetupInput();
        moveAction?.Enable();
        zoomAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        zoomAction?.Disable();

        moveAction?.Dispose();
        zoomAction?.Dispose();
        moveAction = null;
        zoomAction = null;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        HandleMove(dt);
        HandleZoom(dt);
    }

    private void SetupInput()
    {
        if (moveAction != null && zoomAction != null)
        {
            return;
        }

        // WASD (and arrows) + gamepad left stick.
        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        var wasd = moveAction.AddCompositeBinding("2DVector");
        wasd.With("Up", "<Keyboard>/w");
        wasd.With("Down", "<Keyboard>/s");
        wasd.With("Left", "<Keyboard>/a");
        wasd.With("Right", "<Keyboard>/d");

        var arrows = moveAction.AddCompositeBinding("2DVector");
        arrows.With("Up", "<Keyboard>/upArrow");
        arrows.With("Down", "<Keyboard>/downArrow");
        arrows.With("Left", "<Keyboard>/leftArrow");
        arrows.With("Right", "<Keyboard>/rightArrow");

        moveAction.AddBinding("<Gamepad>/leftStick");

        // Mouse scroll wheel (Y). Note: scroll is in "lines"-like units; we scale via zoomSpeed.
        zoomAction = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y", expectedControlType: "Axis");
    }

    private void HandleMove(float dt)
    {
        if (moveAction == null)
        {
            return;
        }

        Vector2 move = moveAction.ReadValue<Vector2>();
        if (move.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 delta;

        if (moveSpace == MoveSpace.WorldXZ)
        {
            // World axes: W = +Z.
            delta = new Vector3(move.x, 0f, move.y) * (moveSpeed * dt);
        }
        else
        {
            Transform reference = moveSpace == MoveSpace.ReferenceTransform ? movementReference : targetCamera != null ? targetCamera.transform : null;
            if (reference == null)
            {
                delta = new Vector3(move.x, 0f, move.y) * (moveSpeed * dt);
            }
            else
            {
                // Project onto XZ plane so movement stays flat.
                // Use forward when possible; if the camera is perfectly top-down, forward projects to ~0,
                // so fall back to using camera.up (screen up direction).
                Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up);
                Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.ProjectOnPlane(reference.up, Vector3.up);
                }

                right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

                delta = (right * move.x + forward * move.y) * (moveSpeed * dt);
            }
        }

        Vector3 next = transform.position + delta;

        if (useBounds)
        {
            next.x = Mathf.Clamp(next.x, boundsX.x, boundsX.y);
            next.z = Mathf.Clamp(next.z, boundsZ.x, boundsZ.y);
        }

        transform.position = next;
    }

    private void HandleZoom(float dt)
    {
        if (zoomAction == null)
        {
            return;
        }

        float scrollY = zoomAction.ReadValue<float>();
        if (Mathf.Abs(scrollY) < 0.001f)
        {
            return;
        }

        if (targetCamera == null)
        {
            return;
        }

        // Positive scrollY typically means "scroll up".
        // Convention: scroll up = zoom IN.
        float zoomDelta = scrollY * zoomSpeed * dt;

        ZoomMode resolvedMode = zoomMode;
        if (resolvedMode == ZoomMode.Auto)
        {
            resolvedMode = targetCamera.orthographic ? ZoomMode.OrthographicSize : ZoomMode.Height;
        }

        switch (resolvedMode)
        {
            case ZoomMode.OrthographicSize:
                targetCamera.orthographic = true;
                targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize - zoomDelta, orthoSizeLimits.x, orthoSizeLimits.y);
                break;

            case ZoomMode.FieldOfView:
                targetCamera.orthographic = false;
                targetCamera.fieldOfView = Mathf.Clamp(targetCamera.fieldOfView - zoomDelta, fovLimits.x, fovLimits.y);
                break;

            case ZoomMode.Height:
            default:
                {
                    // Zoom by changing height only so we don't "slide" on angled cameras.
                    Vector3 pos = transform.position;
                    pos.y = Mathf.Clamp(pos.y - zoomDelta, heightLimits.x, heightLimits.y);
                    transform.position = pos;
                    break;
                }
        }
    }
}
