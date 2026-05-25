using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    public Transform content;
    public GameObject itemPrefab;
    private bool isRefreshing = false;
    private Hotbar _hotbar;
    private PlayerController _playerController;
    private List<HotbarSlot> _inventorySlots = new List<HotbarSlot>();
    [SerializeField] private TextMeshProUGUI feedbackText;

    void Start()
    {
        isRefreshing = false;
        _playerController = PlayerController.Instance;
        GameManager.Instance.OnInventoryChanged += RefreshUI;
        InitializeInventorySlots();
        RefreshUI();
    }

    private void InitializeInventorySlots()
    {
        if (_playerController == null || content == null) return;

        int capacity = _playerController.InventoryCapacity;

        // Clear existing slots
        foreach (Transform child in content)
            Destroy(child.gameObject);

        _inventorySlots.Clear();

        // Create fixed number of slots
        for (int i = 0; i < capacity; i++)
        {
            GameObject obj = Instantiate(itemPrefab, content);
            HotbarSlot slot = obj.GetComponent<HotbarSlot>();
            if (slot != null)
            {
                slot.ClearSlot();
                _inventorySlots.Add(slot);
            }
        }

        Canvas.ForceUpdateCanvases();
        RectTransform rect = content.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    public void RefreshUI()
    {
        if (isRefreshing) return;
        isRefreshing = true;

        if (content == null)
            ResolveContent();

        if (_playerController == null)
            _playerController = PlayerController.Instance;

        if (_inventorySlots.Count == 0)
            InitializeInventorySlots();

        // Clear all slots first
        foreach (var slot in _inventorySlots)
            slot?.ClearSlot();

        // Fill slots from inventory
        var items = GameManager.Instance?.GetItems();
        if (items != null)
        {
            int slotIndex = 0;
            foreach (var (item, amount) in items)
            {
                if (slotIndex >= _inventorySlots.Count) break;
                _inventorySlots[slotIndex].SetItem(item, amount);
                slotIndex++;
            }
        }

        Debug.Log(GameManager.Instance?.GetInventoryItemsAsString());
        isRefreshing = false;
    }

    private void ResolveContent()
    {
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var t in all)
        {
            if (t != null && t.CompareTag("InventoryContent"))
            {
                content = t;
                return;
            }
        }

        content = null;
    }

    public bool TryQuickEquipToHotbar(ItemData item, int amount)
    {
        if (item == null) return false;

        if (_hotbar == null)
            _hotbar = FindFirstObjectByType<Hotbar>(FindObjectsInactive.Include);

        if (_hotbar == null)
        {
            SetFeedback("No hotbar found.");
            return false;
        }

        bool equipped = _hotbar.TryEquipFirstEmpty(item, amount);
        if (!equipped)
            SetFeedback("Hotbar is full. Drag an item to replace a slot.");

        return equipped;
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }
}