using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventoryItemUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler, IPointerEnterHandler
{
    [Header("Display")]
    public Image icon;
    public TextMeshProUGUI itemName;
    public TextMeshProUGUI itemAmount;
    [Tooltip("Optional background square behind the icon.")]
    [SerializeField] private GameObject backgroundOverlay;
    [Tooltip("Keep the background visible even when an item is present.")]
    [SerializeField] private bool keepBackgroundVisible = true;

    // Set by InventoryUI.RefreshUI()
    [HideInInspector] public ItemData itemData;
    [HideInInspector] public int amount;

    // ── Drag state ────────────────────────────────────────────────────────────
    private static GameObject _dragGhost;
    private Canvas _rootCanvas;
    private InventoryUI _inventoryUI;
    private Image _backgroundImage;

    private Canvas RootCanvas
    {
        get
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();
            return _rootCanvas;
        }
    }

    // ── IBeginDragHandler ────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        // If HotbarSlot is on this GameObject, it handles dragging (including inventory mode).
        if (GetComponent<HotbarSlot>() != null)
            return;

        Debug.Log($"[InventoryItemUI] OnBeginDrag — itemData={(itemData != null ? itemData.itemName : "NULL")}, amount={amount}");

        if (itemData == null)
        {
            Debug.LogError("[InventoryItemUI] itemData is null — drag cancelled. Make sure InventoryUI sets itemui.itemData.");
            return;
        }

        if (RootCanvas == null)
        {
            Debug.LogError("[InventoryItemUI] Could not find a parent Canvas — drag cancelled.");
            return;
        }

        // Destroy any leftover ghost from an interrupted drag
        if (_dragGhost != null) Destroy(_dragGhost);

        _dragGhost = new GameObject("InventoryDragGhost");
        _dragGhost.transform.SetParent(RootCanvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();

        Image ghostImg = _dragGhost.AddComponent<Image>();
        ghostImg.sprite = (icon != null) ? icon.sprite : null;
        ghostImg.color = Color.white;
        ghostImg.raycastTarget = false;     // MUST be false — let events pass through to slots

        // If there's no sprite, make it a bright yellow square so you can see it
        if (ghostImg.sprite == null)
        {
            ghostImg.color = Color.yellow;
            Debug.LogWarning("[InventoryItemUI] No sprite on icon — ghost will appear as a yellow square.");
        }

        RectTransform rt = _dragGhost.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60f, 60f);

        // Put DragDropItem on THIS object — Unity sets pointerDrag to the object
        // that received OnBeginDrag, so HotbarSlot.OnDrop reads it from here,
        // not from the ghost.
        DragDropItem payload = gameObject.GetComponent<DragDropItem>();
        if (payload == null) payload = gameObject.AddComponent<DragDropItem>();
        payload.itemData = itemData;
        payload.amount = amount;
        payload.sourceSlot = null;   // came from inventory

        Debug.Log($"[InventoryItemUI] Ghost created. Canvas={RootCanvas.name}, sprite={(ghostImg.sprite != null ? ghostImg.sprite.name : "none")}");

        MoveDragGhost(eventData);
    }

    // ── IDragHandler ─────────────────────────────────────────────────────────
    public void OnDrag(PointerEventData eventData)
    {        // If HotbarSlot is on this GameObject, it handles dragging (including inventory mode).
        if (GetComponent<HotbarSlot>() != null)
            return;
        MoveDragGhost(eventData);
    }

    // ── IEndDragHandler ──────────────────────────────────────────────────────
    public void OnEndDrag(PointerEventData eventData)
    {        // If HotbarSlot is on this GameObject, it handles dragging (including inventory mode).
        if (GetComponent<HotbarSlot>() != null)
            return;
        Debug.Log($"[InventoryItemUI] OnEndDrag — pointerEnter={(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null")}");

        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (itemData == null) return;

        _inventoryUI?.TryQuickEquipToHotbar(itemData, amount);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUIHoverSound();
        else if (UISoundManager.Instance != null)
            UISoundManager.Instance.PlayHoverSound();
    }

    public void BindInventoryUI(InventoryUI inventoryUI)
    {
        _inventoryUI = inventoryUI;
    }

    public void SetItem(ItemData item, int stackAmount)
    {
        itemData = item;
        amount = stackAmount;
        RefreshDisplay();
    }

    public void ClearItem()
    {
        itemData = null;
        amount = 0;
        RefreshDisplay();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void MoveDragGhost(PointerEventData eventData)
    {
        if (_dragGhost == null) return;

        RectTransform canvasRT = RootCanvas.GetComponent<RectTransform>();
        Camera cam = RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventData.pressEventCamera;

        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT, eventData.position, cam, out Vector2 localPoint);

        if (ok)
            _dragGhost.GetComponent<RectTransform>().localPosition = localPoint;
    }

    private void Start()
    {
        if (backgroundOverlay != null)
            _backgroundImage = backgroundOverlay.GetComponent<Image>();

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        bool hasItem = itemData != null;

        if (icon != null)
        {
            icon.sprite = hasItem ? itemData.icon : null;
            Color iconColor = icon.color;
            iconColor.a = hasItem ? 1f : 0f;
            icon.color = iconColor;
        }

        if (_backgroundImage != null)
        {
            Color bgColor = _backgroundImage.color;
            bgColor.a = 1f;
            _backgroundImage.color = bgColor;
        }

        if (itemName != null)
            itemName.gameObject.SetActive(false);

        if (itemAmount != null)
        {
            itemAmount.gameObject.SetActive(hasItem && amount > 1);
            itemAmount.text = hasItem ? "x" + amount : "";
        }

        if (backgroundOverlay != null)
            backgroundOverlay.SetActive(keepBackgroundVisible || !hasItem);
    }
}