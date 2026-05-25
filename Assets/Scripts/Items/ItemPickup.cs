using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [Tooltip("Script should be placed on the item's Prefab.")]
    [SerializeField] public ItemData itemData;
    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupSoundVolume = 1f;
    private const string DefaultPickupSoundPath = "Assets/Audio/PickUp.wav";

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        // GetComponent<Collider>().isTrigger = true;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        TryAssignDefaultPickupSound();
    }

    private void OnValidate()
    {
        TryAssignDefaultPickupSound();
    }

    private void TryAssignDefaultPickupSound()
    {
        if (pickupSound != null)
            return;

        pickupSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultPickupSoundPath);
    }
#endif

    public void Pickup(GameManager gameManager)
    {
        if (gameManager == null || itemData == null)
            return;

        KitchenIngredientController ingredient = GetComponent<KitchenIngredientController>();
        if (ingredient != null && (ingredient.IsCooked() || ingredient.IsBurnt()))
            return;

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupSoundVolume);

        gameManager.AddInventoryItem(itemData);
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;
        if (!other.CompareTag("Pickup"))
            return;

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        Pickup(gameManager);
    }
}