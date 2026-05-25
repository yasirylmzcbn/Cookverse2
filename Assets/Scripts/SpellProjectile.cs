using System.Collections.Generic;
using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float maxLifetimeSeconds = 5f;

    [Header("Damage")]
    [SerializeField] private float areaRadius = 2.5f;
    [SerializeField] private int damage = 1;

    [Header("Collision")]
    [Tooltip("Optional: only explode when hitting these layers. If set to Everything (-1), any collision will explode.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Tooltip("Whether the explosion overlap should include trigger colliders (common for enemies).")]
    [SerializeField] private QueryTriggerInteraction explosionQueryTriggers = QueryTriggerInteraction.Collide;

    private readonly Collider[] _overlapBuffer = new Collider[64];

    private Rigidbody _rb;
    private bool _exploded;
    private Transform _owner;

    private void Awake()
    {
        TryGetComponent(out _rb);
    }

    public void Initialize(Transform owner)
    {
        _owner = owner;

        if (_owner == null) return;

        // Prevent the projectile from immediately colliding with the caster.
        Collider[] ownerCols = _owner.GetComponentsInChildren<Collider>();
        Collider[] myCols = GetComponentsInChildren<Collider>();

        for (int i = 0; i < myCols.Length; i++)
        {
            Collider myCol = myCols[i];
            if (myCol == null) continue;

            for (int j = 0; j < ownerCols.Length; j++)
            {
                Collider ownerCol = ownerCols[j];
                if (ownerCol == null) continue;

                Physics.IgnoreCollision(myCol, ownerCol, true);
            }
        }
    }

    private void Start()
    {
        if (_rb != null)
        {
            // Matches your Bullet.cs pattern (Unity supports linearVelocity in recent versions).
            _rb.linearVelocity = transform.forward * speed;
        }

        if (maxLifetimeSeconds > 0f)
            Destroy(gameObject, maxLifetimeSeconds);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_exploded) return;
        if (collision.collider == null) return;

        // Layer filter
        if (((1 << collision.collider.gameObject.layer) & hitLayers.value) == 0)
            return;

        ExplodeAt(collision.GetContact(0).point);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_exploded) return;
        if (other == null) return;

        // Layer filter
        if (((1 << other.gameObject.layer) & hitLayers.value) == 0)
            return;

        ExplodeAt(other.ClosestPoint(transform.position));
    }

    private void ExplodeAt(Vector3 position)
    {
        if (_exploded) return;
        _exploded = true;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySpellProjectileImpactSound();

        int count = Physics.OverlapSphereNonAlloc(position, areaRadius, _overlapBuffer, ~0, explosionQueryTriggers);

        // Prevent damaging the same enemy multiple times due to multiple colliders.
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

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);
        Gizmos.DrawSphere(transform.position, areaRadius);
    }
#endif
}
