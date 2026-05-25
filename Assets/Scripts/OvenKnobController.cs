using UnityEngine;

/// <summary>
/// Oven-specific knob controller.
///
/// Extends KnobController and overrides OnStateChanged to also toggle the
/// oven indicator lights through every OvenSlot in the cookwareSlots array.
///
/// Everything else (rotation, angle clamping, IsOn propagation to slots,
/// stove visuals) is handled by the base KnobController unchanged.
///
/// Inspector setup
/// ───────────────
///   • Set Cookware Type  → Oven  (in the base KnobController header)
///   • Assign the single OvenSlot to Cookware Slots[0]
///   • The OvenSlot itself owns the Door Animator and Lights Animator references
/// </summary>
public class OvenKnobController : KnobController
{
    /// <summary>
    /// Called by the base class whenever the on/off state changes.
    /// Drives the oven lights on all attached OvenSlots.
    /// </summary>
    protected override void OnStateChanged(bool newState)
    {
        base.OnStateChanged(newState);
        foreach (CookwareSlot slot in cookwareSlots)
        {
            if (slot == null) continue;
            // try convert to ovenslot
            if (slot is OvenSlot ovenSlot)
            {
                Debug.Log($"OvenKnobController: Setting lights on slot {ovenSlot.name} to {newState}");
                ovenSlot.SetLights(newState);
            }
        }
    }
}