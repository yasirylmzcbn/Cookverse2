using UnityEngine;
using System.Collections;

public class MeleeEnemy : Enemy
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackCooldown = 1.5f;

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

    void Attack() {
        if (player == null)
            return;

        if (player.IsDeadOrDown)
            return;

        player.TakeDamage(damage);

    }
}