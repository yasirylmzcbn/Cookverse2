using UnityEngine;
using System.Collections;

public class RangedEnemy : Enemy
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Projectile")]
    [SerializeField] private GameObject enemyBulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 10f;

    private PlayerController player;
    private Transform playerTransform;
    private bool canAttack;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        canAttack = true;
    }

    protected override void CheckAttack()
    {
        if (!canAttack) return;
        if (player == null || playerTransform == null) return;
        if (player.IsDeadOrDown) return;

        float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;

        if (sqrDistance <= attackRange * attackRange)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        canAttack = false;

        if (player != null && !player.IsDeadOrDown)
        {
            Attack();
            PlayAttackSound();
            GetComponent<EnemyMovementAI>().AttackAnimation();
        }

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }

    //void Attack()
    //{
    //    if (enemyBulletPrefab == null || firePoint == null) return;

    //    Vector3 direction = (playerTransform.position - firePoint.position).normalized;

    //    GameObject bullet = Instantiate(
    //        enemyBulletPrefab,
    //        firePoint.position,
    //        Quaternion.LookRotation(direction)
    //    );

    //    Rigidbody rb = bullet.GetComponent<Rigidbody>();
    //    if (rb != null)
    //    {
    //        rb.linearVelocity = direction * bulletSpeed;
    //    }

    //    bullet.GetComponent<EnemyBullet>().damage = damage;
    //}

    void Attack()
    {
        if (enemyBulletPrefab == null || firePoint == null) return;
        if (player == null || playerTransform == null) return;
        if (player.IsDeadOrDown) return;

        //Vector3 targetPos = playerTransform.position;
        Vector3 targetPos = playerTransform.position - Vector3.up * 1.3f;
        Vector3 startPos = firePoint.position;

        Vector3 launchVelocity;
        if (!CalculateLaunchVelocity(startPos, targetPos, bulletSpeed, out launchVelocity))
        {
            // Target is too far for the given speed, just aim directly as fallback
            launchVelocity = (targetPos - startPos).normalized * bulletSpeed;
        }

        GameObject bullet = Instantiate(
            enemyBulletPrefab,
            startPos,
            Quaternion.LookRotation(launchVelocity)
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.linearVelocity = launchVelocity;
        }

        bullet.GetComponent<EnemyBullet>().damage = damage;
    }

    bool CalculateLaunchVelocity(Vector3 start, Vector3 target, float speed, out Vector3 velocity)
    {
        Vector3 toTarget = target - start;
        float g = Physics.gravity.magnitude;

        // Flatten to get horizontal distance
        Vector3 toTargetFlat = new Vector3(toTarget.x, 0, toTarget.z);
        float horizontalDist = toTargetFlat.magnitude;
        float verticalDist = toTarget.y;

        // Calculate launch angle using projectile motion formula
        float speedSq = speed * speed;
        float discriminant = speedSq * speedSq - g * (g * horizontalDist * horizontalDist + 2 * verticalDist * speedSq);

        if (discriminant < 0)
        {
            velocity = Vector3.zero;
            return false; // Out of range
        }

        // Use the lower angle for a flatter, faster throw
        float angle = Mathf.Atan2(speedSq - Mathf.Sqrt(discriminant), g * horizontalDist);

        Vector3 flatDirection = toTargetFlat.normalized;
        velocity = flatDirection * speed * Mathf.Cos(angle) + Vector3.up * speed * Mathf.Sin(angle);
        return true;
    }
}