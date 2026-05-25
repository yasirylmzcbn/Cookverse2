using UnityEngine;

public class MinimapCameraLimits : MonoBehaviour
{
    public static MinimapCameraLimits Instance { get; private set; }

    public GameObject player;
    public float yLimit;
    private Transform _playerTransform;

    private void Awake()
    {
        // Ensure only one minimap camera exists across scene loads
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate MinimapCamera detected. Keeping '{Instance.gameObject.name}', destroying '{gameObject.name}'.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!TryResolvePlayerTransform())
            return;

        Vector3 p = _playerTransform.position;
        transform.position = new Vector3(p.x, yLimit, p.z);
    }

    private bool TryResolvePlayerTransform()
    {
        if (_playerTransform != null)
            return true;

        if (player != null)
        {
            _playerTransform = player.transform;
            return _playerTransform != null;
        }

        if (PlayerController.Instance != null)
        {
            player = PlayerController.Instance.gameObject;
            _playerTransform = PlayerController.Instance.transform;
            return _playerTransform != null;
        }

        PlayerController foundPlayerController = FindFirstObjectByType<PlayerController>();
        if (foundPlayerController != null)
        {
            player = foundPlayerController.gameObject;
            _playerTransform = foundPlayerController.transform;
            return _playerTransform != null;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            player = taggedPlayer;
            _playerTransform = taggedPlayer.transform;
            return _playerTransform != null;
        }

        return false;
    }
}