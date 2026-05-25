using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cookverse/Spells/AoE Damage Spell", fileName = "AoEDamageSpell")]
public class AoEDamageSpell : SpellDefinition
{
    [Header("AoE")]
    [Min(0f)]
    public float radius = 3f;

    [Min(0)]
    public int damage = 3;

    [Tooltip("Which layers to consider for hits. Enemies should be on one of these layers.")]
    public LayerMask hitMask = ~0;

    private readonly Collider[] _overlapBuffer = new Collider[64];

    public override string displayName => "Ground Slam";
    public override string description => $"Hit the ground to deal {damage} damage to all enemies within {radius} units.";

    public override void Cast(in SpellCastContext context)
    {
        if (context.player == null) return;

        Vector3 center = context.player.transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

        HashSet<Enemy> damaged = new HashSet<Enemy>();
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (!damaged.Add(enemy)) continue;

            enemy.Damage(damage);
        }
    }
}
