using UnityEngine;

[CreateAssetMenu(menuName = "Cookverse/Inventory/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    [Tooltip("Will probably be removed or obselete if we don't limit slots")]
    public bool stackable;
    [Header("Representation")]
    public Sprite icon;
    public GameObject dropPrefab;
}