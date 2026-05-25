using Cookverse.Assets.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;

public class KitchenIngredientController : MonoBehaviour
{
    public enum CookedVisualMode
    {
        ChoppedMaterialSwap,
        CookedMesh
    }

    [Header("Ingredient")]
    [SerializeField] private Ingredient ingredientType;

    public Ingredient IngredientType => ingredientType;
    public bool IsProteinIngredient => ingredientType == Ingredient.WerewolfSteak ||
            ingredientType == Ingredient.WerewolfTail ||
            ingredientType == Ingredient.YetiRibs ||
            ingredientType == Ingredient.YetiDrumstick;
    public bool IsVegetableIngredient => !IsProteinIngredient;

    [Header("Forms")]
    [SerializeField] private GameObject rawForm;
    [SerializeField] private GameObject choppedForm;
    [SerializeField] private CookedVisualMode cookedVisualMode = CookedVisualMode.ChoppedMaterialSwap;
    [SerializeField] private Material cookedMaterial;
    [SerializeField] private Material burntMaterial;
    [SerializeField] private GameObject cookedForm;

    [Header("Dragging")]
    [Tooltip("Fixed kitchen camera (assign in Inspector).")]
    public Camera kitchenCamera;

    [Tooltip("What layers can be interacted with (ingredients + kitchen items).")]
    public LayerMask interactLayers = ~0;

    [Header("Drag Surface")]
    public LayerMask dragSurfaceLayers = ~0;
    public float surfaceRayDistance = 1000f;
    public bool useFixedDragY = false;
    public float fixedDragY = 0.9f;
    public float hoverYOffset = 0.02f;

    [SerializeField] private Rigidbody rb;

    [Header("Snapping Assist")]
    [Tooltip("Bigger = easier to 'hit' kitchen items with the cursor (sphere cast).")]
    public float snapSphereCastRadius = 0.25f;

    [Tooltip("How far we search for nearby slots while dragging (performance).")]
    public float slotSearchRadius = 2.0f;

    [Header("Drag Visuals")]
    [Tooltip("If true, while dragging and within snap range of a slot, hide this ingredient's own renderers (slot ghost preview remains visible).")]
    public bool hideDraggedVisualsInSnapRange = true;

    private readonly Collider[] _snapOverlapBuffer = new Collider[32];
    private readonly RaycastHit[] _clickHitBuffer = new RaycastHit[32];

    private bool isDragging;
    private Vector3 dragOffset;
    private float lockedY;

    private Transform dragOriginalParent;

    private bool isPreviewSnapped;
    private IngredientSlotBehaviour previewSnappedSlot;

    private HotbarSlot sourceHotbarSlot;
    private ItemData sourceHotbarItem;
    private bool spawnedFromHotbar;

    private CookwareSlot lidHoverSlot;
    private bool lidHoverInRange;

    private IngredientSlotBehaviour currentSlot;
    private IngredientSlotBehaviour hoverSlot;

    public float cookLevel = 0f; // 0 = raw, 1 = cooked, 1.75 = burnt

    private Renderer[] choppedRenderers;
    private Material[][] choppedOriginalMaterials;
    private Renderer[] cookedRenderers;
    private Material[][] cookedOriginalMaterials;

    // Remembers where the ingredient was last sitting freely (counter/table/etc.)
    private struct FreeState
    {
        public bool hasValue;
        public Transform parent;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public bool rbKinematic;
        public bool rbUseGravity;
        public RigidbodyConstraints rbConstraints;
    }

    private FreeState freeState;

    private void Awake()
    {
        if (rb == null) TryGetComponent(out rb);
        CacheChoppedMaterials();
        CacheCookedMaterials();
        SaveFreeState();
    }

    private void CacheChoppedMaterials()
    {
        GameObject choppedTarget = GetChoppedVisualObject();
        if (choppedTarget == null)
        {
            choppedRenderers = System.Array.Empty<Renderer>();
            choppedOriginalMaterials = System.Array.Empty<Material[]>();
            return;
        }

        choppedRenderers = choppedTarget.GetComponentsInChildren<Renderer>(true);
        choppedOriginalMaterials = new Material[choppedRenderers.Length][];

        for (int i = 0; i < choppedRenderers.Length; i++)
            choppedOriginalMaterials[i] = GetRendererMaterials(choppedRenderers[i]);
    }

    private void CacheCookedMaterials()
    {
        GameObject cookedTarget = GetCookedVisualObject();
        if (cookedTarget == null)
        {
            cookedRenderers = System.Array.Empty<Renderer>();
            cookedOriginalMaterials = System.Array.Empty<Material[]>();
            return;
        }

        cookedRenderers = cookedTarget.GetComponentsInChildren<Renderer>(true);
        cookedOriginalMaterials = new Material[cookedRenderers.Length][];

        for (int i = 0; i < cookedRenderers.Length; i++)
            cookedOriginalMaterials[i] = GetRendererMaterials(cookedRenderers[i]);
    }

    private void RestoreChoppedOriginalMaterials()
    {
        if (choppedRenderers == null || choppedOriginalMaterials == null) return;

        for (int i = 0; i < choppedRenderers.Length && i < choppedOriginalMaterials.Length; i++)
        {
            if (choppedRenderers[i] == null || choppedOriginalMaterials[i] == null) continue;
            SetRendererMaterials(choppedRenderers[i], choppedOriginalMaterials[i]);
        }
    }

    private void RestoreCookedOriginalMaterials()
    {
        if (cookedRenderers == null || cookedOriginalMaterials == null) return;

        for (int i = 0; i < cookedRenderers.Length && i < cookedOriginalMaterials.Length; i++)
        {
            if (cookedRenderers[i] == null || cookedOriginalMaterials[i] == null) continue;
            SetRendererMaterials(cookedRenderers[i], cookedOriginalMaterials[i]);
        }
    }

    private void ApplyMaterialToChopped(Material material)
    {
        if (material == null || choppedRenderers == null) return;

        for (int i = 0; i < choppedRenderers.Length; i++)
        {
            if (choppedRenderers[i] == null) continue;

            Material[] mats = GetRendererMaterials(choppedRenderers[i]);
            for (int m = 0; m < mats.Length; m++)
                mats[m] = material;

            SetRendererMaterials(choppedRenderers[i], mats);
        }
    }

    private void ApplyMaterialToCooked(Material material)
    {
        if (material == null || cookedRenderers == null) return;

        for (int i = 0; i < cookedRenderers.Length; i++)
        {
            if (cookedRenderers[i] == null) continue;

            Material[] mats = GetRendererMaterials(cookedRenderers[i]);
            for (int m = 0; m < mats.Length; m++)
                mats[m] = material;

            SetRendererMaterials(cookedRenderers[i], mats);
        }
    }

    private static Material[] GetRendererMaterials(Renderer renderer)
    {
        if (renderer == null) return System.Array.Empty<Material>();
        return IsPrefabObject(renderer) ? renderer.sharedMaterials : renderer.materials;
    }

    private static void SetRendererMaterials(Renderer renderer, Material[] materials)
    {
        if (renderer == null || materials == null) return;

        if (IsPrefabObject(renderer))
            renderer.sharedMaterials = materials;
        else
            renderer.materials = materials;
    }

    private static bool IsPrefabObject(Renderer renderer)
    {
        return renderer == null || !renderer.gameObject.scene.IsValid();
    }

    private static bool IsValidSceneObject(GameObject go)
    {
        return go != null && go.scene.IsValid();
    }

    private GameObject GetChoppedVisualObject()
    {
        if (IsValidSceneObject(choppedForm))
            return choppedForm;

        if (choppedForm != null && rawForm != null)
            Debug.LogWarning($"[{name}] choppedForm is not a valid scene object. Falling back to rawForm.");

        return rawForm;
    }

    private GameObject GetCookedVisualObject()
    {
        return IsValidSceneObject(cookedForm) ? cookedForm : null;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        if (!SwitchCamera.IsKitchenInteractionAllowed())
        {
            if (isDragging)
            {
                isDragging = false;
                ExitPreviewSnap();
                CloseAnyCookwareLid();
                SetHoverSlot(null);
            }
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryBeginDrag();

        if (isDragging)
            DragMove();

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
            EndDragAndTryPlace();
    }

    private void TryBeginDrag()
    {
        if (kitchenCamera == null) return;

        if (!IsTopmostIngredientUnderPointer())
            return;


        if (currentSlot != null)
        {
            currentSlot.RemoveIngredient(this);
            currentSlot = null;

            BeginDragInternal();
            return;
        }

        BeginDragInternal();
    }

    /// <summary>
    /// Called by HotbarWorldDrop after spawning this ingredient under the cursor.
    /// Skips the raycast ownership check (not needed — we just created this object)
    /// </summary>
    public void BeginDragFromHotbar()
    {
        BeginDragInternal(forceFixedHeight: true);
    }

    public void SetHotbarSource(HotbarSlot hotbarSlot, ItemData itemData)
    {
        sourceHotbarSlot = hotbarSlot;
        sourceHotbarItem = itemData;
        spawnedFromHotbar = hotbarSlot != null && itemData != null;
    }

    private bool IsTopmostIngredientUnderPointer()
    {
        if (kitchenCamera == null || Mouse.current == null) return false;

        Ray ray = kitchenCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        KitchenIngredientController bestIngredient = null;
        float bestDistance = float.PositiveInfinity;

        int hitCount = Physics.RaycastNonAlloc(ray, _clickHitBuffer, 500f, interactLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _clickHitBuffer[i].collider;
            if (col == null) continue;

            KitchenIngredientController ingredient = col.GetComponentInParent<KitchenIngredientController>();
            if (ingredient == null) continue;

            float d = _clickHitBuffer[i].distance;
            if (d < bestDistance)
            {
                bestDistance = d;
                bestIngredient = ingredient;
            }
        }

        // Fallback to a small sphere cast to be more forgiving for tiny colliders.
        if (bestIngredient == null)
        {
            hitCount = Physics.SphereCastNonAlloc(ray, snapSphereCastRadius, _clickHitBuffer, 500f, interactLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _clickHitBuffer[i].collider;
                if (col == null) continue;

                KitchenIngredientController ingredient = col.GetComponentInParent<KitchenIngredientController>();
                if (ingredient == null) continue;

                float d = _clickHitBuffer[i].distance;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestIngredient = ingredient;
                }
            }
        }

        return bestIngredient == this;
    }

    private void BeginDragInternal(bool forceFixedHeight = false)
    {
        isDragging = true;

        dragOriginalParent = transform.parent;

        lockedY = (forceFixedHeight || useFixedDragY) ? fixedDragY : transform.position.y;

        if (forceFixedHeight)
        {
            Vector3 pos = transform.position;
            pos.y = lockedY + hoverYOffset;
            transform.position = pos;
        }

        if (!TryGetPointerWorldPoint(out Vector3 pointerWorld))
            dragOffset = Vector3.zero;

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // transform.SetParent(null, true);
        SetHoverSlot(null);
    }

    private void DragMove()
    {
        if (!TryGetPointerWorldPoint(out Vector3 pointerWorld))
            return;

        if (!isPreviewSnapped)
        {
            Vector3 newPos = pointerWorld + dragOffset;
            newPos.y = lockedY + hoverYOffset;
            transform.position = newPos;
        }

        UpdateHoverPreview(pointerWorld);
    }

    private void UpdateHoverPreview(Vector3 pointerWorld)
    {
        // Prefer slot under cursor (for intent), else nearest in range
        IngredientSlotBehaviour candidate = SphereCastSlotUnderMouse();

        if (candidate == null)
            candidate = FindNearestAcceptingSlotInRange(pointerWorld);

        Debug.Log("Hover candidate: " + (candidate != null ? candidate.name : "null"));
        SetHoverSlot(candidate);
        UpdateHoverSlotPreviewVisibility(pointerWorld);
        UpdateCookwareLidHover(pointerWorld);
        UpdateDraggedVisualsVisibility();
    }

    private void UpdateCookwareLidHover(Vector3 pointerWorld)
    {
        if (!isDragging)
            return;

        CookwareSlot cookwareSlot = hoverSlot as CookwareSlot;
        bool inRange = cookwareSlot != null && cookwareSlot.IsWithinSnapRange(pointerWorld);

        if (cookwareSlot == lidHoverSlot && inRange == lidHoverInRange)
            return;

        if (lidHoverSlot != null && (cookwareSlot != lidHoverSlot || !inRange))
            lidHoverSlot.NotifyDragOutOfSnapRangeOrDropped();

        if (cookwareSlot != null && inRange)
            cookwareSlot.NotifyDragInSnapRange();

        lidHoverSlot = cookwareSlot;
        lidHoverInRange = inRange;
    }

    private void CloseAnyCookwareLid()
    {
        if (lidHoverSlot != null)
            lidHoverSlot.NotifyDragOutOfSnapRangeOrDropped();

        lidHoverSlot = null;
        lidHoverInRange = false;
    }

    private void UpdateHoverSlotPreviewVisibility(Vector3 pointerWorld)
    {
        if (hoverSlot == null)
        {
            ExitPreviewSnap();
            return;
        }

        if (!hoverSlot.CanAcceptIngredient(this) || !hoverSlot.IsWithinSnapRange(pointerWorld))
        {
            ExitPreviewSnap();
            return;
        }

        Transform anchor = null;

        if (hoverSlot is OvenSlot ovenSlot)
            anchor = ovenSlot.GetPreviewAnchor(pointerWorld, this);
        else if (hoverSlot is ISingleAnchorIngredientSlot singleAnchorSlot)
            anchor = singleAnchorSlot.GetAnchor();
        else if (hoverSlot is IDualAnchorIngredientSlot dualAnchorSlot)
            anchor = IsProteinIngredient ? dualAnchorSlot.GetProteinAnchor() : dualAnchorSlot.GetVegetableAnchor();

        if (anchor != null)
            EnterPreviewSnap(hoverSlot, anchor);
        else
            ExitPreviewSnap();
    }

    private void UpdateDraggedVisualsVisibility()
    {
        if (!isDragging)
        {
            return;
        }

        if (!hideDraggedVisualsInSnapRange)
        {
            return;
        }

        bool shouldHide = hoverSlot != null
                          && hoverSlot.IsWithinSnapRange(transform.position)
                          && hoverSlot.CanAcceptIngredient(this);
    }

    private void EndDragAndTryPlace()
    {
        Debug.Log("Ending drag of " + name);
        isDragging = false;

        CloseAnyCookwareLid();

        Vector3 pointerWorld = transform.position;
        bool hasPointerWorld = TryGetPointerWorldPoint(out pointerWorld);

        // If we were preview-snapped, align back to pointer so SnapInto() doesn't save the anchor as free state.
        if (isPreviewSnapped)
        {
            ExitPreviewSnap();
            if (hasPointerWorld)
                AlignToPointer(pointerWorld);
        }

        // Try place into hovered slot if in its snap range
        if (hoverSlot != null
            && (!hasPointerWorld || hoverSlot.IsWithinSnapRange(pointerWorld))
            && hoverSlot.TryPlaceIngredient(this))
        {
            currentSlot = hoverSlot;
            spawnedFromHotbar = false;
            sourceHotbarSlot = null;
            sourceHotbarItem = null;
            SetHoverSlot(null);
            return;
        }

        // Not placed: keep where dropped
        SetHoverSlot(null);

        if (spawnedFromHotbar)
        {
            ReturnToHotbarInventory();
            Destroy(gameObject);
            return;
        }

        if (rb != null)
            rb.isKinematic = false;

        SaveFreeState();
    }

    private void SetHoverSlot(IngredientSlotBehaviour slot)
    {
        if (hoverSlot == slot) return;

        ExitPreviewSnap();

        if (hoverSlot is CookwareSlot previousCookwareSlot)
        {
            previousCookwareSlot.NotifyDragOutOfSnapRangeOrDropped();
            previousCookwareSlot.HidePreview();
        }

        hoverSlot = slot;
    }

    private void EnterPreviewSnap(IngredientSlotBehaviour slot, Transform anchor)
    {
        if (!isDragging) return;
        if (slot == null || anchor == null) return;

        if (isPreviewSnapped && previewSnappedSlot == slot && transform.parent == anchor)
            return;

        ExitPreviewSnap();

        isPreviewSnapped = true;
        previewSnappedSlot = slot;

        Vector3 worldScale = transform.lossyScale;
        transform.SetParent(anchor, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(
            worldScale.x / anchor.lossyScale.x,
            worldScale.y / anchor.lossyScale.y,
            worldScale.z / anchor.lossyScale.z
        );

        if (rb != null)
            rb.isKinematic = true;
    }

    private void ExitPreviewSnap()
    {
        if (!isPreviewSnapped)
            return;

        transform.SetParent(dragOriginalParent, true);
        isPreviewSnapped = false;
        previewSnappedSlot = null;
    }

    private void AlignToPointer(Vector3 pointerWorld)
    {
        Vector3 newPos = pointerWorld + dragOffset;
        newPos.y = lockedY + hoverYOffset;
        transform.position = newPos;
    }

    private IngredientSlotBehaviour SphereCastSlotUnderMouse()
    {
        if (kitchenCamera == null) return null;

        Ray ray = kitchenCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.SphereCast(ray, snapSphereCastRadius, out RaycastHit hit, 500f, interactLayers, QueryTriggerInteraction.Ignore))
            return hit.collider != null ? hit.collider.GetComponentInParent<IngredientSlotBehaviour>() : null;

        return null;
    }

    private IngredientSlotBehaviour FindNearestAcceptingSlotInRange(Vector3 fromWorldPos)
    {
        int count = Physics.OverlapSphereNonAlloc(fromWorldPos, slotSearchRadius, _snapOverlapBuffer, interactLayers, QueryTriggerInteraction.Ignore);

        IngredientSlotBehaviour best = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider col = _snapOverlapBuffer[i];
            if (col == null) continue;

            IngredientSlotBehaviour slot = col.GetComponentInParent<IngredientSlotBehaviour>();
            if (slot == null) continue;
            if (!slot.CanAcceptIngredient(this)) continue;

            float d = slot.DistanceToAnchor(fromWorldPos, this);
            if (d <= slot.SnapRange && d < bestDist)
            {
                bestDist = d;
                best = slot;
            }
        }

        return best;
    }

    private bool TryGetPointerWorldPoint(out Vector3 world)
    {
        world = default;
        if (kitchenCamera == null || Mouse.current == null) return false;

        Ray ray = kitchenCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, surfaceRayDistance, dragSurfaceLayers, QueryTriggerInteraction.Ignore))
        {
            world = hit.point;
            return true;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0f, lockedY, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    private void SaveFreeState()
    {
        freeState.hasValue = true;
        freeState.parent = transform.parent;
        freeState.position = transform.position;
        freeState.rotation = transform.rotation;
        freeState.localScale = transform.localScale;

        if (rb != null)
        {
            freeState.rbKinematic = rb.isKinematic;
            freeState.rbUseGravity = rb.useGravity;
            freeState.rbConstraints = rb.constraints;
        }
    }

    private void RestoreFreeState()
    {
        if (!freeState.hasValue) return;

        transform.SetParent(freeState.parent, true);
        transform.position = freeState.position;
        transform.rotation = freeState.rotation;
        transform.localScale = freeState.localScale;

        if (rb != null)
        {
            rb.isKinematic = freeState.rbKinematic;
            rb.useGravity = freeState.rbUseGravity;
            rb.constraints = freeState.rbConstraints;
        }
    }

    public void SnapInto(Transform anchor)
    {
        SaveFreeState();
        if (anchor != null)
        {
            if (!IsCooked() && !IsBurnt())
                SetToChoppedForm();
            Vector3 worldScale = transform.lossyScale;
            transform.SetParent(anchor, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            transform.localScale = new Vector3(
        worldScale.x / anchor.lossyScale.x,
        worldScale.y / anchor.lossyScale.y,
        worldScale.z / anchor.lossyScale.z
    );
        }

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    public void OnRemovedFromSlot()
    {
        RestoreFreeState();
        if (!IsCooked() && !IsBurnt()) SetToRawForm();
    }

    private void ReturnToHotbarInventory()
    {
        if (sourceHotbarSlot != null)
            sourceHotbarSlot.RestoreDroppedIngredient(sourceHotbarItem);

        sourceHotbarSlot = null;
        sourceHotbarItem = null;
        spawnedFromHotbar = false;
    }

    public void SetToRawForm()
    {
        GameObject choppedTarget = GetChoppedVisualObject();
        GameObject cookedTarget = GetCookedVisualObject();

        if (rawForm != null) rawForm.SetActive(true);
        if (choppedTarget != null && choppedTarget != rawForm) choppedTarget.SetActive(false);
        if (cookedTarget != null) cookedTarget.SetActive(false);
        RestoreChoppedOriginalMaterials();
        RestoreCookedOriginalMaterials();
    }

    public void SetToChoppedForm()
    {
        GameObject choppedTarget = GetChoppedVisualObject();
        GameObject cookedTarget = GetCookedVisualObject();

        if (rawForm != null) rawForm.SetActive(choppedTarget == rawForm);
        if (choppedTarget != null) choppedTarget.SetActive(true);
        if (cookedTarget != null) cookedTarget.SetActive(false);
        RestoreChoppedOriginalMaterials();
        RestoreCookedOriginalMaterials();
    }

    public void SetToCookedForm()
    {
        GameObject choppedTarget = GetChoppedVisualObject();
        GameObject cookedTarget = GetCookedVisualObject();

        if (rawForm != null) rawForm.SetActive(false);

        if (cookedVisualMode == CookedVisualMode.CookedMesh && cookedTarget != null)
        {
            if (choppedTarget != null && choppedTarget != rawForm) choppedTarget.SetActive(false);
            cookedTarget.SetActive(true);
            RestoreCookedOriginalMaterials();
        }
        else
        {
            if (rawForm != null) rawForm.SetActive(choppedTarget == rawForm);
            if (choppedTarget != null) choppedTarget.SetActive(true);
            if (cookedTarget != null) cookedTarget.SetActive(false);
            ApplyMaterialToChopped(cookedMaterial);
        }
    }

    public void SetToBurntForm()
    {
        GameObject choppedTarget = GetChoppedVisualObject();
        GameObject cookedTarget = GetCookedVisualObject();

        if (rawForm != null) rawForm.SetActive(false);

        if (cookedVisualMode == CookedVisualMode.CookedMesh && cookedTarget != null)
        {
            if (choppedTarget != null && choppedTarget != rawForm) choppedTarget.SetActive(false);
            cookedTarget.SetActive(true);
            ApplyMaterialToCooked(burntMaterial);
        }
        else
        {
            if (rawForm != null) rawForm.SetActive(choppedTarget == rawForm);
            if (choppedTarget != null) choppedTarget.SetActive(true);
            if (cookedTarget != null) cookedTarget.SetActive(false);
            ApplyMaterialToChopped(burntMaterial);
        }
    }

    public bool IsCooked() => cookLevel >= GV.REQUIRED_COOK_LEVEL && cookLevel < GV.REQUIRED_BURN_LEVEL;

    public bool IsBurnt() => cookLevel >= GV.REQUIRED_BURN_LEVEL;

}