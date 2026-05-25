using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMove : MonoBehaviour
{
    private const float YMin = -50.0f;
    private const float YMax = 50.0f;

    [Header("References")]
    public Transform lookAt;
    public Transform Player;

    [Header("Camera")]
    public float distance = 10.0f;
    public float sensivity = 65.0f;

    [Header("Collision")]
    [Tooltip("Layers the camera should collide with (e.g., Default, Environment).")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Tooltip("Radius used for collision checks. Larger values keep the camera further from walls.")]
    [SerializeField] private float collisionRadius = 0.25f;

    [Tooltip("Extra space to keep between the camera and the hit surface.")]
    [SerializeField] private float collisionOffset = 0.1f;

    [Tooltip("Minimum distance the camera is allowed to get to the pivot.")]
    [SerializeField] private float minDistance = 0.5f;

    [Tooltip("How quickly the camera returns to the desired distance after an obstruction clears.")]
    [SerializeField] private float collisionSmooth = 12f;

    [Tooltip("Mouse delta is in pixels; this scales it to feel closer to the old Input.GetAxis.")]
    [SerializeField] private float mouseDeltaMultiplier = 0.2f;

    [Header("Input (New Input System)")]
    [SerializeField] private InputAction lookAction;

    private float currentX = 0.0f;
    private float currentY = 0.0f;
    private float currentDistance;

    private void Awake()
    {
        EnsureActionsConfigured();
        currentDistance = Mathf.Max(minDistance, distance);
    }

    private void OnEnable()
    {
        lookAction?.Enable();
    }

    private void OnDisable()
    {
        // DO NOT disable lookAction here - Unity Input System bug with Mouse Delta freezing permanently
        // lookAction?.Disable();
    }

    private void EnsureActionsConfigured()
    {
        if (lookAction != null && lookAction.bindings.Count > 0)
            return;

        lookAction = new InputAction("Look", InputActionType.Value);

        // Mouse delta (pixels per frame)
        lookAction.AddBinding("<Mouse>/delta");

        // Gamepad right stick (-1..1)
        lookAction.AddBinding("<Gamepad>/rightStick");
    }

    private void LateUpdate()
    {
        if (lookAt == null) return;
        if (Time.timeScale == 0f) return;

        Vector2 lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        // Mouse delta needs extra scaling vs stick.
        // (Stick is already normalized; mouse is pixels.)
        float scale = Time.deltaTime * sensivity;
        Vector2 scaled = lookInput * scale;

        if (Mouse.current != null && Mouse.current.delta.IsActuated())
            scaled *= mouseDeltaMultiplier;

        currentX += scaled.x;
        currentY -= scaled.y;

        currentY = Mathf.Clamp(currentY, YMin, YMax);

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0f);

        Vector3 pivot = lookAt.position;
        float targetDistance = Mathf.Max(minDistance, distance);

        Vector3 desiredOffset = rotation * new Vector3(0f, 0f, -targetDistance);
        Vector3 desiredPosition = pivot + desiredOffset;

        Vector3 castVector = desiredPosition - pivot;
        float castDistance = castVector.magnitude;
        Vector3 castDirection = castDistance > 0.0001f ? (castVector / castDistance) : Vector3.back;

        float correctedDistance = targetDistance;
        if (castDistance > 0.0001f)
        {
            if (Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    castDirection,
                    out RaycastHit hit,
                    castDistance,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                correctedDistance = Mathf.Clamp(hit.distance - collisionOffset, minDistance, targetDistance);
            }
        }

        // Smooth the distance to reduce camera popping when occlusion appears/disappears.
        float smoothT = 1f - Mathf.Exp(-Mathf.Max(0.01f, collisionSmooth) * Time.deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, correctedDistance, smoothT);

        Vector3 finalOffset = rotation * new Vector3(0f, 0f, -currentDistance);
        transform.position = pivot + finalOffset;
        transform.LookAt(pivot);
        Player.rotation = Quaternion.Euler(0f, currentX, 0f);
    }
}