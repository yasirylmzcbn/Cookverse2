using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemy;

    public void SpawnEnemy(float multiplier)
    {
        GameObject enemyObject = Instantiate(enemy, transform.position, transform.rotation);
        enemyObject.GetComponent<Enemy>().SetHealthMultiplier(multiplier);
    }
}
