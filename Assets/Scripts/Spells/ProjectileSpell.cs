using UnityEngine;

[CreateAssetMenu(menuName = "Cookverse/Spells/Projectile Spell", fileName = "ProjectileSpell")]
public class ProjectileSpell : SpellDefinition
{
    [Header("Projectile")]
    [Tooltip("Prefab with SpellProjectile + Rigidbody + Collider (recommended).")]
    public GameObject projectilePrefab;

    [Tooltip("How far in front of the origin the projectile appears (avoids colliding with player).")]
    [Min(0f)]
    public float forwardOffset = 0.8f;
    public override string displayName => "Projectile";
    public override string description => $"Cast a projectile that can damage enemies.";
    public override bool CanCast(in SpellCastContext context)
    {
        return projectilePrefab != null;
    }

    public override void Cast(in SpellCastContext context)
    {
        if (projectilePrefab == null) return;

        Transform origin = context.origin != null ? context.origin : (context.player != null ? context.player.transform : null);
        if (origin == null) return;

        Vector3 forward = context.forwardFlat.sqrMagnitude > 0.0001f ? context.forwardFlat : origin.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 spawnPos = origin.position + forward * forwardOffset;
        Quaternion spawnRot = Quaternion.LookRotation(forward, Vector3.up);

        GameObject instance = Object.Instantiate(projectilePrefab, spawnPos, spawnRot);

        if (instance != null && context.player != null)
        {
            SpellProjectile spellProjectile = instance.GetComponent<SpellProjectile>();
            if (spellProjectile != null)
                spellProjectile.Initialize(context.player.transform);
        }
    }
}
