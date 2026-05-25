using UnityEngine;

/// <summary>
/// Drag payload used by spell UI drag/drop interactions.
/// </summary>
public class SpellDragDropItem : MonoBehaviour
{
    public SpellDefinition spell;

    /// <summary>
    /// Diamond slot this drag came from, or -1 when dragged from the spell list.
    /// </summary>
    public int sourceDiamondSlot = -1;
}
