using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// A single hotbar slot. Attach to each of the 3 slot GameObjects under your Hotbar panel.
///
/// Setup in Inspector:
///   - slotIcon        → Image child showing the item sprite (alpha 0 when empty)
///   - slotAmountText  → TMP text showing "x2", hidden when empty
///   - emptyOverlay    → optional Image shown when slot is empty
///   - worldDrop       → assign the HotbarWorldDrop component (on same or parent GO)
/// </summary>
public class HotbarSlot : MonoBehaviour,
    IDropHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler
{
    [Header("Display references")]
    [SerializeField] private Image slotIcon;
    [SerializeField] private TextMeshProUGUI slotAmountText;
    [Tooltip("Optional visual shown behind the slot content (e.g. your empty square background).")]
    [SerializeField] private GameObject emptyOverlay;
    [Tooltip("If enabled, keeps the background overlay visible even when this slot has an item.")]
    [SerializeField] private bool keepBackgroundVisible = true;

    [Header("World drop")]
    [Tooltip("Assign the HotbarWorldDrop component (on the Hotbar GO or this GO).")]
    [SerializeField] private HotbarWorldDrop worldDrop;

    // ── Runtime state ─────────────────────────────────────────────────────────
    public ItemData HeldItem { get; private set; }
    public int HeldAmount { get; private set; }
    public bool IsEmpty => HeldItem == null;

    // ── Drag state ────────────────────────────────────────────────────────────
    private static GameObject _dragGhost;
    private Canvas _rootCanvas;
    private bool _dragLeftUI;   // true once the ghost has left all UI elements
    private bool _worldDragHandedOff;
    private Image _emptyOverlayImage;
    private bool _dragVisualHidden;

    private GameManager gameManager;

    private Canvas RootCanvas
    {
        get
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();
            return _rootCanvas;
        }
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Start()
    {
        gameManager = GameManager.Instance;
        if (emptyOverlay != null)
            _emptyOverlayImage = emptyOverlay.GetComponent<Image>();

        EnsureVisualOrder();
        Debug.Log("gamemanager: " + (gameManager != null ? "FOUND" : "NULL"));
        RefreshDisplay();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetItem(ItemData item, int amount)
    {
        HeldItem = item;
        HeldAmount = amount;
        RefreshDisplay();
    }

    public void ClearSlot()
    {
        HeldItem = null;
        HeldAmount = 0;
        RefreshDisplay();
    }

    // ── IDropHandler ─────────────────────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        DragDropItem payload = eventData.pointerDrag.GetComponent<DragDropItem>();
        if (payload == null) return;

        ItemData incomingItem = payload.itemData;
        int incomingAmount = payload.amount;
        HotbarSlot sourceSlot = payload.sourceSlot;

        if (sourceSlot != null && sourceSlot != this && incomingItem != null && incomingAmount > 0)
        {
            ItemData tempItem = HeldItem;
            int tempAmount = HeldAmount;

            SetItem(incomingItem, incomingAmount);

            if (tempItem != null)
                sourceSlot.SetItem(tempItem, tempAmount);
            else
                sourceSlot.ClearSlot();

            sourceSlot.CompleteSuccessfulDrag();
        }

        if (sourceSlot == null && incomingItem != null && incomingAmount > 0)
        {
            SetItem(incomingItem, incomingAmount);
        }
        
        // Play drop sound via SoundManager
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUIDropSound();
    }

    // ── IBeginDragHandler / IDragHandler / IEndDragHandler ───────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsEmpty) return;

        // Play drag start sound via SoundManager
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUIDragStartSound();

        _dragLeftUI = false;
        _worldDragHandedOff = false;
        SetDragVisualHidden(true);

        // In kitchen interaction mode, skip UI ghost dragging and hand off
        // directly to world ingredient drag so one gesture handles everything.
        if (SwitchCamera.IsKitchenInteractionAllowed() && TryBeginWorldDragFromHotbar())
        {
            _worldDragHandedOff = true;
            SetDragVisualHidden(false);
            return;
        }

        _dragGhost = new GameObject("HotbarDragGhost");
        _dragGhost.transform.SetParent(RootCanvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();

        Image ghostImg = _dragGhost.AddComponent<Image>();
        ghostImg.sprite = slotIcon != null ? slotIcon.sprite : null;
        ghostImg.color = new Color(1f, 1f, 1f, 0.75f);
        ghostImg.raycastTarget = false;

        RectTransform rt = _dragGhost.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60f, 60f);

        // Put DragDropItem on THIS object — Unity's pointerDrag points here, not the ghost.
        DragDropItem payload = gameObject.GetComponent<DragDropItem>();
        if (payload == null) payload = gameObject.AddComponent<DragDropItem>();
        payload.itemData = HeldItem;
        payload.amount = HeldAmount;
        payload.sourceSlot = this;

        MoveDragGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_worldDragHandedOff)
            return;

        MoveDragGhost(eventData);

        // Track the moment the cursor leaves all UI — marks this as a world-drop gesture.
        if (!_dragLeftUI && !IsPointerOverUI(eventData))
            _dragLeftUI = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
        }

        if (_worldDragHandedOff)
            return;

        // Always restore visual — whether drop was accepted or not
        SetDragVisualHidden(false);

        // Cursor released over the game world (not a UI drop target) → world drop.
        // If it ended on a UI slot, OnDrop() on that slot already handled it.
        if (_dragLeftUI && !IsPointerOverUI(eventData))
            DropIntoWorld();
    }

    // ── IPointerClickHandler ─────────────────────────────────────────────────
    /// <summary>Right-click clears the slot.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            ClearSlot();
    }

    // ── World drop ────────────────────────────────────────────────────────────
    private void DropIntoWorld()
    {
        if (IsEmpty) return;

        // Lazy resolve — HotbarWorldDrop may be on the parent Hotbar GO
        if (worldDrop == null)
            worldDrop = GetComponentInParent<HotbarWorldDrop>();

        if (worldDrop == null)
        {
            Debug.LogWarning("[HotbarSlot] No HotbarWorldDrop found — cannot spawn ingredient in kitchen.");
            return;
        }

        ItemData droppedItem = HeldItem;
        KitchenIngredientController ingredient = worldDrop.SpawnAndBeginDrag(droppedItem, this);
        if (ingredient == null)
            return;

        // reduce the ingredient stack only after the world object successfully spawns
        HeldAmount--;
        if (HeldAmount <= 0)
            ClearSlot();
        else
            RefreshDisplay();

        if (gameManager != null)
            gameManager.RemoveInventoryItem(droppedItem);
    }

    private bool TryBeginWorldDragFromHotbar()
    {
        if (IsEmpty) return false;

        if (worldDrop == null)
            worldDrop = GetComponentInParent<HotbarWorldDrop>();

        if (worldDrop == null)
            return false;

        ItemData droppedItem = HeldItem;
        KitchenIngredientController ingredient = worldDrop.SpawnAndBeginDrag(droppedItem, this);
        if (ingredient == null)
            return false;

        HeldAmount--;
        if (HeldAmount <= 0)
            ClearSlot();
        else
            RefreshDisplay();

        if (gameManager != null)
            gameManager.RemoveInventoryItem(droppedItem);

        return true;
    }

    public void RestoreDroppedIngredient(ItemData item)
    {
        if (item == null)
            return;

        if (HeldItem == null)
            HeldItem = item;

        HeldAmount++;
        RefreshDisplay();

        if (gameManager != null)
            gameManager.AddInventoryItem(item);
    }

    public void CompleteSuccessfulDrag()
    {
        SetDragVisualHidden(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// True if the pointer is currently over any UI element inside this Canvas.
    /// Uses EventSystem's current raycast result — no extra raycasts needed.
    /// </summary>
    private bool IsPointerOverUI(PointerEventData eventData)
    {
        GameObject go = eventData.pointerCurrentRaycast.gameObject;
        if (go == null) return false;
        return go.GetComponentInParent<Canvas>() != null;
    }

    private void MoveDragGhost(PointerEventData eventData)
    {
        if (_dragGhost == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventData.pressEventCamera,
            out Vector2 localPoint
        );
        _dragGhost.GetComponent<RectTransform>().localPosition = localPoint;
    }

    // ── Display ───────────────────────────────────────────────────────────────
    private void RefreshDisplay()
    {
        bool hasItem = !IsEmpty;
        bool showContents = hasItem && !_dragVisualHidden;

        EnsureVisualOrder();

        if (slotIcon != null)
        {
            slotIcon.sprite = hasItem ? HeldItem.icon : null;
            Color c = slotIcon.color;
            c.a = showContents ? 1f : 0f;
            slotIcon.color = c;
        }

        if (_emptyOverlayImage != null)
        {
            Color bg = _emptyOverlayImage.color;
            bg.a = 1f;
            _emptyOverlayImage.color = bg;
        }

        if (slotAmountText != null)
        {
            slotAmountText.gameObject.SetActive(showContents && HeldAmount > 1);
            slotAmountText.text = hasItem ? "x" + HeldAmount : "";
        }

        if (emptyOverlay != null)
            emptyOverlay.SetActive(keepBackgroundVisible || !hasItem);
    }

    private void SetDragVisualHidden(bool hidden)
    {
        _dragVisualHidden = hidden;
        RefreshDisplay();
    }

    /// <summary>
    /// Keeps the background square behind the item icon so both stay visible.
    /// </summary>
    private void EnsureVisualOrder()
    {
        if (emptyOverlay == null || slotIcon == null)
            return;

        RectTransform bg = emptyOverlay.transform as RectTransform;
        RectTransform icon = slotIcon.transform as RectTransform;
        if (bg == null || icon == null)
            return;

        if (bg.parent != icon.parent)
            return;

        if (bg.GetSiblingIndex() >= icon.GetSiblingIndex())
            bg.SetSiblingIndex(Mathf.Max(0, icon.GetSiblingIndex() - 1));
    }
}