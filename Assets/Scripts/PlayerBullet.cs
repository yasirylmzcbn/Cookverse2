using System;
using UnityEngine;

public class PlayerBullet : MonoBehaviour
{
    public float speed;
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

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null)
            return;

        enemy.Damage(1);
        Destroy(gameObject);
    }
}
