using UnityEngine;

/// <summary>
/// Manages the hotbar panel. Attach to your "Hotbar" GameObject.
///
/// Setup:
///   1. Create 3 child GameObjects inside the Hotbar panel (e.g. Slot0, Slot1, Slot2).
///   2. Add a HotbarSlot component to each child.
///   3. Add an Image (for the icon) and a TextMeshPro text (for x2, x3…) as children of each slot.
///   4. Assign those children to HotbarSlot's slotIcon / slotAmountText fields in the Inspector.
///   5. Assign the 3 HotbarSlot components to this script's `slots` array in the Inspector.
///   6. Make sure your Canvas has a GraphicRaycaster component (required for UI drag-drop).
/// </summary>
public class Hotbar : MonoBehaviour
{
    [Tooltip("Assign the 3 HotbarSlot components here.")]
    [SerializeField] private HotbarSlot[] slots = new HotbarSlot[3];

    public int SlotCount => slots != null ? slots.Length : 0;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the item held in a given slot (0-based), or null if empty.</summary>
    public ItemData GetSlotItem(int index)
    {
        if (!IsValidIndex(index)) return null;
        return slots[index].HeldItem;
    }

    /// <summary>Returns the amount held in a given slot.</summary>
    public int GetSlotAmount(int index)
    {
        if (!IsValidIndex(index)) return 0;
        return slots[index].HeldAmount;
    }

    /// <summary>Programmatically place an item into a slot (useful for saving/loading).</summary>
    public void SetSlot(int index, ItemData item, int amount)
    {
        if (!IsValidIndex(index)) return;
        slots[index].SetItem(item, amount);
    }

    /// <summary>Clear a specific slot.</summary>
    public void ClearSlot(int index)
    {
        if (!IsValidIndex(index)) return;
        slots[index].ClearSlot();
    }

    /// <summary>Clear all slots.</summary>
    public void ClearAll()
    {
        foreach (var slot in slots)
            slot?.ClearSlot();
    }

    /// <summary>
    /// Places the item in the first empty slot. Returns false if all slots are filled.
    /// </summary>
    public bool TryEquipFirstEmpty(ItemData item, int amount)
    {
        if (item == null || slots == null) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].IsEmpty)
            {
                slots[i].SetItem(item, amount);
                return true;
            }
        }

        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private bool IsValidIndex(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
        {
            Debug.LogWarning($"[Hotbar] Invalid slot index {index}");
            return false;
        }
        return true;
    }
}