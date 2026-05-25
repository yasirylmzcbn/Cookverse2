using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this to the same GameObject as HotbarSlot (or a parent).
///
/// When the player drags a hotbar item off the UI into the kitchen view, this
/// script spawns the ItemData's dropPrefab at the cursor's world position and
/// immediately begins a kitchen drag on it — so it behaves exactly like any
/// other ingredient already sitting on the counter.
///
/// Requirements:
///   - The spawned prefab must have a KitchenIngredientController on it.
///   - KitchenIngredientController.kitchenCamera must be assigned (either in
///     the prefab Inspector or via the FindCamera fallback below).
///   - The hotbar slot's item stays (infinite use, consumed only by recipe).
/// </summary>
public class HotbarWorldDrop : MonoBehaviour
{
    [Tooltip("The kitchen camera used for raycasting. If null, falls back to finding KitchenCameraController in scene.")]
    [SerializeField] private Camera kitchenCamera;

    [Tooltip("Layer mask passed to the spawned ingredient for drag-surface raycasting.")]
    [SerializeField] private LayerMask dragSurfaceLayers = ~0;

    [Tooltip("Layer mask passed to the spawned ingredient for interact/snap raycasting.")]
    [SerializeField] private LayerMask interactLayers = ~0;

    // Called by HotbarSlot when the UI drag leaves the canvas into the scene.
    // Returns the spawned ingredient so HotbarSlot can forget about it.
    public KitchenIngredientController SpawnAndBeginDrag(ItemData itemData, HotbarSlot sourceSlot)
    {
        if (!SwitchCamera.IsKitchenInteractionAllowed())
        {
            Debug.Log("[HotbarWorldDrop] Ignoring world-drop spawn while not in kitchen view.");
            return null;
        }

        if (itemData == null || itemData.dropPrefab == null)
        {
            Debug.LogWarning($"[HotbarWorldDrop] {itemData?.itemName} has no dropPrefab — cannot spawn in kitchen.");
            return null;
        }

        Camera cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning("[HotbarWorldDrop] No kitchen camera found.");
            return null;
        }

        // ── Spawn at cursor world position ────────────────────────────────────
        Vector3 spawnPos = GetCursorWorldPosition(cam);

        // disable is trigger from prefab collider
        itemData.dropPrefab.GetComponent<Collider>().isTrigger = false;
        GameObject spawned = Instantiate(itemData.dropPrefab, spawnPos, Quaternion.identity);

        KitchenIngredientController ingredient = spawned.GetComponent<KitchenIngredientController>();
        if (ingredient == null)
        {
            Debug.LogWarning($"[HotbarWorldDrop] dropPrefab '{itemData.dropPrefab.name}' has no KitchenIngredientController.");
            Destroy(spawned);
            return null;
        }

        // ── Wire up the camera and layer masks if not already set in prefab ──
        if (ingredient.kitchenCamera == null)
            ingredient.kitchenCamera = cam;

        ingredient.SetHotbarSource(sourceSlot, itemData);

        // ── Tell the ingredient to start dragging immediately ─────────────────
        // We call the public entry point that mirrors what Update() does on
        // mouse-down, but bypass the IsTopmostIngredientUnderPointer() check
        // because the object was just spawned and hasn't had a physics frame yet.
        ingredient.BeginDragFromHotbar();

        return ingredient;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Camera ResolveCamera()
    {
        if (kitchenCamera != null) return kitchenCamera;

        KitchenCameraController camController = FindFirstObjectByType<KitchenCameraController>();
        if (camController != null)
        {
            Camera cam = camController.GetComponent<Camera>();
            if (cam != null) return cam;
        }

        return Camera.main;
    }

    private Vector3 GetCursorWorldPosition(Camera cam)
    {
        if (Mouse.current == null) return Vector3.zero;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        // Try hitting the kitchen surface first
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, dragSurfaceLayers, QueryTriggerInteraction.Ignore))
            return hit.point;

        // Fallback: flat Y=0 plane
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return Vector3.zero;
    }
}