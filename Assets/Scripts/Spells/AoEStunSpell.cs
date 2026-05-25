using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cookverse/Spells/AoE Stun Spell", fileName = "AoEStunSpell")]
public class AoEStunSpell : SpellDefinition
{
    [Header("AoE")]
    [Min(0f)]
    public float radius = 3f;

    [Min(0)]
    public float duration = 5f;

    [Tooltip("Which layers to consider for hits. Enemies should be on one of these layers.")]
    public LayerMask hitMask = ~0;

    private readonly Collider[] _overlapBuffer = new Collider[64];

    public override string displayName => "Lightning Bolt";
    public override string description => $"Stun all enemies within {radius} units.";

    public override void Cast(in SpellCastContext context)
    {
        if (context.player == null) return;

        Vector3 center = context.player.transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

        HashSet<Enemy> stunned = new HashSet<Enemy>();
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (!stunned.Add(enemy)) continue; //added already

            enemy.Stun(duration);
        }
    }
}
