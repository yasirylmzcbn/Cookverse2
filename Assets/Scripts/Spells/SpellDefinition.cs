using UnityEngine;
using System.Text;

public readonly struct SpellCastContext
{
    public readonly PlayerController player;
    public readonly Transform origin;
    public readonly Vector3 forwardFlat;

    public SpellCastContext(PlayerController player, Transform origin, Vector3 forwardFlat)
    {
        this.player = player;
        this.origin = origin;
        this.forwardFlat = forwardFlat;
    }
}

public abstract class SpellDefinition : ScriptableObject
{
    [Header("UI")]
    public Sprite icon;

    // Script-defined display name. Override if you want a custom name.
    // Default is derived from the concrete spell class name (e.g., "PlayerAoEDamageSpell" -> "Player AoE Damage Spell").
    public virtual string displayName => HumanizeTypeName(GetType().Name);
    public virtual string description => "placeholder description";

    [Header("Unlock")]
    [Tooltip("If enabled, the player must unlock the recipe below before this spell can be cast. This being disabled does not automatically add it to the user's inventory.")]
    public bool requiresRecipeUnlock;
    public Recipe requiredRecipe;

    [Header("Cooldown")]
    [Min(0f)]
    public float cooldownSeconds = 10f;

    public virtual bool CanCast(in SpellCastContext context) => true;

    public abstract void Cast(in SpellCastContext context);

    private static string HumanizeTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "Spell";

        // Strip common suffix.
        if (typeName.EndsWith("Spell"))
            typeName = typeName.Substring(0, typeName.Length - "Spell".Length);

        StringBuilder sb = new StringBuilder(typeName.Length + 8);
        for (int i = 0; i < typeName.Length; i++)
        {
            char c = typeName[i];
            char prev = i > 0 ? typeName[i - 1] : '\0';
            char next = i + 1 < typeName.Length ? typeName[i + 1] : '\0';

            bool isUpper = char.IsUpper(c);
            bool prevIsUpper = i > 0 && char.IsUpper(prev);
            bool prevIsLower = i > 0 && char.IsLower(prev);
            bool nextIsLower = i + 1 < typeName.Length && char.IsLower(next);

            // Insert a space:
            // - between lower->upper transitions ("TimedBuff")
            // - before the last capital in an acronym when it transitions to lower ("AoE" + "Damage" => "AoE Damage")
            if (i > 0 && isUpper && (prevIsLower || (prevIsUpper && nextIsLower)))
                sb.Append(' ');

            sb.Append(c);
        }

        // Add suffix back for clarity.
        sb.Append(" Spell");
        return sb.ToString();
    }
}
