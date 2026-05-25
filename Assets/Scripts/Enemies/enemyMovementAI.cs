using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyMovementAI : MonoBehaviour
{
    private Transform player;
    private NavMeshAgent navMeshAgent;
    private Animator anim;
    private Enemy attackScript;
    bool moving = true;
    float lastSpeed = -1f;
    private bool isStunned = false;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        attackScript = GetComponent<Enemy>();
        anim = GetComponentInChildren<Animator>();
        navMeshAgent.stoppingDistance = attackScript.AttackRange * 0.9f;
    }

    void Update()
    {
        if (player == null || isStunned) return;
        navMeshAgent.SetDestination(player.position);
        moving = true;
        if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            Vector3 targetPos = player.position;
            targetPos.y = transform.position.y;
            transform.LookAt(targetPos);
            moving = false;
        }
        float speedValue = moving ? 1f : 0f;
        if (anim != null && speedValue != lastSpeed)
        {
            anim.SetFloat("Speed", speedValue);
            lastSpeed = speedValue;
        }
    }

    public void AttackAnimation()
    {
        anim?.SetTrigger("Attack");
    }

    public void StunMovement()
    {
        isStunned = true;
        navMeshAgent.isStopped = true;
        anim?.SetFloat("Speed", 0f);
        lastSpeed = 0f;
    }

    public void RestoreMovement()
    {
        isStunned = false;
        navMeshAgent.isStopped = false;
    }

    public void ApplyKnockback(Vector3 force, float duration)
    {
        StartCoroutine(KnockbackCoroutine(force, duration));
    }

    private IEnumerator KnockbackCoroutine(Vector3 force, float duration)
    {
        navMeshAgent.enabled = false;
        isStunned = true;
        anim?.SetFloat("Speed", 0f);
        lastSpeed = 0f;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.freezeRotation = true;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            Debug.Log($"[Knockback] {gameObject.name} | force={force} | magnitude={force.magnitude}");
            rb.AddForce(force, ForceMode.Impulse);
        }

        float timeout = duration;

        yield return new WaitForSeconds(0.5f);

        while (timeout > 0f)
        {
            bool grounded = Physics.Raycast(transform.position, Vector3.down, 0.2f);
            bool notMovingUp = rb.linearVelocity.y <= 0.1f;

            if (grounded && notMovingUp)
                break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        // Let physics settle before snapping to NavMesh
        yield return new WaitForFixedUpdate();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            transform.position = hit.position;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        navMeshAgent.enabled = true;
        isStunned = false;
        navMeshAgent.isStopped = false;
    }
}