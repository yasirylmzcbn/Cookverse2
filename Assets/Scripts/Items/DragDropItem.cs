using UnityEngine;

/// <summary>
/// Lightweight data carrier attached to the drag-ghost GameObject.
/// Both InventoryItemUI and HotbarSlot read/write this during drag operations.
/// </summary>
public class DragDropItem : MonoBehaviour
{
    public ItemData itemData;
    public int amount;

    /// <summary>The HotbarSlot this drag originated from, or null if it came from inventory.</summary>
    public HotbarSlot sourceSlot;
}