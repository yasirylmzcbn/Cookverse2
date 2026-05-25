using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cookverse/Spells/AoE Knockback Spell", fileName = "AoEKnockbackSpell")]
public class AoEKnockbackSpell : SpellDefinition
{
    [Header("AoE")]
    [Min(0f)]
    public float radius = 3f;
    [Tooltip("Which layers to consider for hits. Enemies should be on one of these layers.")]
    public LayerMask hitMask = ~0;

    [Header("Knockback")]
    [Min(0f)]
    [Tooltip("How strongly enemies are launched away from the player.")]
    public float knockbackForce = 10f;
    [Min(0f)]
    [Tooltip("How much force is directed upward (gives the 'fly' effect).")]
    public float knockbackUpwardBias = 5f;

    private readonly Collider[] _overlapBuffer = new Collider[64];

    public override string displayName => "Forcefield";
    public override string description => $"Create a forcefield that pushes enemies within {radius} units away.";

    public override void Cast(in SpellCastContext context)
    {
        if (context.player == null) return;

        Vector3 center = context.player.transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, hitMask, QueryTriggerInteraction.Ignore);

        HashSet<Enemy> pushed = new HashSet<Enemy>();

        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            Enemy enemy = col.GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            if (!pushed.Add(enemy)) continue;

            Vector3 flatDir = enemy.transform.position - center;
            flatDir.y = 0f;

            if (flatDir.sqrMagnitude < 0.001f)
                flatDir = enemy.transform.forward;

            flatDir.Normalize();

            Vector3 knockbackDir = (flatDir + Vector3.up * knockbackUpwardBias).normalized;
            Vector3 force = knockbackDir * knockbackForce;

            // Hand off to the AI component � it handles disabling the agent
            EnemyMovementAI ai = enemy.GetComponent<EnemyMovementAI>();
            if (ai != null)
                ai.ApplyKnockback(force, 1.2f); // 1.2s is usually enough for the arc
        }
    }
}