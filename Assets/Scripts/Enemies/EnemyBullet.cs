using System;
using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public float speed;
    public int damage;
    void Start()
    {
        GetComponent<Rigidbody>().linearVelocity = transform.forward * speed;
    }

    void Update()
    {
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        if (!other.CompareTag("Player")) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (player.IsDeadOrDown)
        {
            Destroy(gameObject);
            return;
        }

        player.TakeDamage(damage);
        Destroy(gameObject);
    }
}
