using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Win : MonoBehaviour
{
    private TMP_Text winText;
    private bool hasWon = false;
    public WaveManager waveManager;

    [SerializeField] private TMP_Text waveNumberText;

    [SerializeField] private GameObject portal;
    private Transform playerTransform;
    [SerializeField] private float portalDistanceFromPlayer = 2f;

    public void UpdateWaveText(int currentWave, int maxWave)
    {
        if (waveNumberText != null)
        {
            waveNumberText.text = $"Wave: {currentWave} / {maxWave}";
        }
    }

    void Start()
    {
        winText = GetComponent<TMP_Text>();
        Debug.Log(winText != null);
        if (winText != null)
        {
            winText.enabled = false;
        }
        waveManager = FindFirstObjectByType<WaveManager>();

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (playerTransform == null)
        {
            Debug.LogError("Player transform not found. Make sure the player GameObject has the 'Player' tag.");
        }
    }

    void Update()
    {
        //Debug.Log(!hasWon);
        //Debug.Log(spawner.IsDoneSpawning());
        //Debug.Log(spawner.EnemyCount());
        if (!hasWon && waveManager != null && waveManager.IsDoneSpawning() && FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length == 0)
        {
            ShowWin();
            if (portal != null && playerTransform != null)
            {
                Vector3 forward = playerTransform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 portalPosition = playerTransform.position + forward * portalDistanceFromPlayer;
                portalPosition.y = 3.34f;
                portal.transform.position = portalPosition;
                portal.SetActive(true);
            }
        }
    }

    void ShowWin()
    {
        hasWon = true;

        if (winText != null)
        {
            winText.enabled = true;
            winText.text = "You Win!";
        }
    }
}