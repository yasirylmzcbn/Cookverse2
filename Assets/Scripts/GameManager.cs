using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum Difficulty
    {
        None,
        Easy,
        Medium,
        Hard,
        Boss
    }
    public static GameManager Instance;
    float waveMultiplierIncrement = 1.1f;
    private int currentWave = 1;
    private int lastWave = 2;
    private int firstWave = 1;
    [SerializeField] private Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();
    public event Action OnInventoryChanged;
    private Difficulty lastCompletedDifficulty = Difficulty.None;
    private Difficulty currentDifficulty = Difficulty.None;

    [Header("Save/Load")]
    [Tooltip("Add all your ItemData assets here so they can be matched when loading.")]
    [SerializeField] private List<ItemData> allItemsDatabase = new List<ItemData>();
    private readonly Dictionary<string, ItemData> _itemLookup = new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);

    void Start()
    {
        //added for debugging
        if (allItemsDatabase != null && allItemsDatabase.Count >= 2)
        {
            items[allItemsDatabase[0]] = 1;
            items[allItemsDatabase[1]] = 1;
            OnInventoryChanged?.Invoke();
        }
    }

    public Difficulty GetLastCompletedDifficulty()
    {
        return lastCompletedDifficulty;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void UnlockAllDifficultiesForTesting()
    {
        lastCompletedDifficulty = Difficulty.Boss;
    }
#endif

    public void SaveGame(int slot)
    {
        PlayerPrefs.SetInt($"Cookverse_Difficulty_{slot}", (int)lastCompletedDifficulty);

        List<InventoryEntry> entries = new List<InventoryEntry>();

        foreach (var pair in items)
        {
            if (pair.Key != null)
            {
                entries.Add(new InventoryEntry
                {
                    assetName = pair.Key.name,
                    itemName = pair.Key.itemName,
                    amount = pair.Value
                });
            }
        }

        List<int> unlockedRecipes = new List<int>();
        if (PlayerRecipeUnlocks.Instance != null)
        {
            foreach (Recipe recipe in PlayerRecipeUnlocks.Instance.GetUnlockedRecipes())
                unlockedRecipes.Add((int)recipe);
        }

        List<SpellSlotEntry> spellSlots = BuildSpellSlotEntries();
        string saveJson = JsonUtility.ToJson(new InventorySaveData
        {
            entries = entries,
            unlockedRecipes = unlockedRecipes,
            spellSlots = spellSlots
        });
        PlayerPrefs.SetString($"Cookverse_Inventory_{slot}", saveJson);
        PlayerPrefs.Save();
        Debug.Log($"Game Saved to Slot {slot}!");
    }

    public void LoadGame(int slot)
    {
        if (PlayerPrefs.HasKey($"Cookverse_Difficulty_{slot}"))
        {
            lastCompletedDifficulty = (Difficulty)PlayerPrefs.GetInt($"Cookverse_Difficulty_{slot}");
        }

        if (PlayerPrefs.HasKey($"Cookverse_Inventory_{slot}"))
        {
            string saveJson = PlayerPrefs.GetString($"Cookverse_Inventory_{slot}");
            InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(saveJson);

            items.Clear();
            if (data != null)
            {
                RebuildItemLookup();
                List<InventoryEntry> entriesToLoad = data.entries;

                if ((entriesToLoad == null || entriesToLoad.Count == 0)
                    && data.names != null && data.amounts != null)
                {
                    entriesToLoad = new List<InventoryEntry>();
                    int legacyCount = Mathf.Min(data.names.Count, data.amounts.Count);
                    for (int i = 0; i < legacyCount; i++)
                    {
                        entriesToLoad.Add(new InventoryEntry
                        {
                            assetName = data.names[i],
                            itemName = data.names[i],
                            amount = data.amounts[i]
                        });
                    }
                }

                if (entriesToLoad != null)
                {
                    foreach (InventoryEntry entry in entriesToLoad)
                    {
                        ItemData item = ResolveItem(entry.assetName, entry.itemName);
                        if (item != null)
                        {
                            if (items.ContainsKey(item))
                                items[item] += entry.amount;
                            else
                                items[item] = entry.amount;
                        }
                        else
                        {
                            Debug.LogWarning($"[GameManager] Could not resolve saved item '{entry.assetName}' / '{entry.itemName}'.");
                        }
                    }
                }

                RestoreRecipeUnlocks(data.unlockedRecipes);
                RestoreSpellSlots(data.spellSlots);
            }
            OnInventoryChanged?.Invoke();
        }
        Debug.Log($"Game Loaded from Slot {slot}!");
    }

    [Serializable]
    private class InventorySaveData
    {
        public List<InventoryEntry> entries;
        public List<string> names;
        public List<int> amounts;
        public List<int> unlockedRecipes;
        public List<SpellSlotEntry> spellSlots;
    }

    [Serializable]
    private class InventoryEntry
    {
        public string assetName;
        public string itemName;
        public int amount;
    }

    [Serializable]
    private class SpellSlotEntry
    {
        public int slotIndex;
        public string spellName;
        public int recipe = -1;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildItemLookup();
        SetEasyDifficulty();
    }

    public void AddInventoryItem(ItemData item, int amount = 1)
    {
        if (items.ContainsKey(item))
        {
            items[item] += amount;
        }
        else
        {
            items[item] = amount;
        }
        OnInventoryChanged?.Invoke();
        Debug.Log(GetInventoryItemsAsString());
    }

    public void RemoveInventoryItem(ItemData item, int amount = 1)
    {
        if (!items.ContainsKey(item))
        {
            return;
        }

        items[item] -= amount;

        if (items[item] <= 0)
        {
            items.Remove(item);
        }
        OnInventoryChanged?.Invoke();
        Debug.Log(GetInventoryItemsAsString());
    }

    public int GetInventoryAmount(ItemData item)
    {
        return items.TryGetValue(item, out int amount) ? amount : 0;
    }

    public string GetInventoryItemsAsString()
    {
        if (items.Count == 0)
            return "Inventory is empty.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("Inventory Contents:");

        foreach (KeyValuePair<ItemData, int> pair in items)
        {
            string itemName = pair.Key != null ? pair.Key.itemName : "NULL ITEM";
            sb.AppendLine($"{itemName} x{pair.Value}");
        }

        return sb.ToString();
    }

    public IReadOnlyDictionary<ItemData, int> GetItems()
    {
        return new Dictionary<ItemData, int>(items);
    }

    public void EnsureStartingWave()
    {
        switch (currentDifficulty)
        {
            case Difficulty.None:
            case Difficulty.Easy:
                SetEasyDifficulty();
                break;

            case Difficulty.Medium:
                SetMediumDifficulty();
                break;

            case Difficulty.Hard:
                SetHardDifficulty();
                break;

            case Difficulty.Boss:
                SetBossDifficulty();
                break;
        }
    }

    public void WaveCompleted()
    {
        currentWave++;
        if (currentWave >= lastWave)
        {
            if (currentDifficulty > lastCompletedDifficulty)
            {
                lastCompletedDifficulty = currentDifficulty;
            }
        }
    }

    public float GetWaveMultiplier()
    {
        return MathF.Pow(waveMultiplierIncrement, currentWave);
    }

    public int CurrentWave()
    {
        return currentWave;
    }

    public int LastWave()
    {
        return lastWave;
    }

    public int DisplayWaveCurrent()
    {
        int total = DisplayWaveMax();
        return Mathf.Clamp((currentWave - firstWave) + 1, 1, total);
    }

    public int DisplayWaveMax()
    {
        return Mathf.Max(1, (lastWave - firstWave) + 1);
    }

    public void SetEasyDifficulty()
    {
        firstWave = 1;
        currentWave = 1;
        lastWave = 3;
        currentDifficulty = Difficulty.Easy;
    }

    public void SetMediumDifficulty()
    {
        firstWave = 10;
        currentWave = 10;
        lastWave = 15;
        currentDifficulty = Difficulty.Medium;
    }

    public void SetHardDifficulty()
    {
        firstWave = 20;
        currentWave = 20;
        lastWave = 27;
        currentDifficulty = Difficulty.Hard;
    }

    public void SetBossDifficulty()
    {
        firstWave = 30;
        currentWave = 30;
        lastWave = 39;
        currentDifficulty = Difficulty.Boss;
    }

    private void RebuildItemLookup()
    {
        _itemLookup.Clear();

        foreach (ItemData item in allItemsDatabase)
        {
            RegisterItem(item);
        }

        foreach (ItemData item in Resources.LoadAll<ItemData>(""))
        {
            RegisterItem(item);
        }

        foreach (ItemData item in Resources.FindObjectsOfTypeAll<ItemData>())
        {
            RegisterItem(item);
        }

        foreach (ItemPickup pickup in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pickup != null)
                RegisterItem(pickup.itemData);
        }

        foreach (ItemData item in items.Keys)
        {
            RegisterItem(item);
        }
    }

    private ItemData ResolveItem(string assetName, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(assetName)
            && _itemLookup.TryGetValue(assetName, out ItemData byAssetName))
        {
            return byAssetName;
        }

        if (!string.IsNullOrWhiteSpace(displayName)
            && _itemLookup.TryGetValue(displayName, out ItemData byDisplayName))
        {
            return byDisplayName;
        }

        return null;
    }

    private void RegisterItem(ItemData item)
    {
        if (item == null)
            return;

        if (!string.IsNullOrWhiteSpace(item.name) && !_itemLookup.ContainsKey(item.name))
            _itemLookup[item.name] = item;

        if (!string.IsNullOrWhiteSpace(item.itemName) && !_itemLookup.ContainsKey(item.itemName))
            _itemLookup[item.itemName] = item;
    }

    private List<SpellSlotEntry> BuildSpellSlotEntries()
    {
        List<SpellSlotEntry> result = new List<SpellSlotEntry>();
        PlayerController player = PlayerController.Instance;
        if (player == null)
            return result;

        SpellDefinition[] loadout = player.GetLoadout();
        if (loadout == null)
            return result;

        for (int i = 0; i < loadout.Length; i++)
        {
            SpellDefinition spell = loadout[i];
            SpellSlotEntry entry = new SpellSlotEntry
            {
                slotIndex = i,
                spellName = spell != null ? spell.name : string.Empty,
                recipe = spell != null ? (int)spell.requiredRecipe : -1
            };
            result.Add(entry);
        }

        return result;
    }

    private void RestoreRecipeUnlocks(List<int> unlockedRecipeValues)
    {
        if (PlayerRecipeUnlocks.Instance == null || unlockedRecipeValues == null)
            return;

        List<Recipe> recipes = new List<Recipe>();
        foreach (int value in unlockedRecipeValues)
        {
            if (Enum.IsDefined(typeof(Recipe), value))
                recipes.Add((Recipe)value);
        }

        PlayerRecipeUnlocks.Instance.SetUnlockedRecipes(recipes);
    }

    private void RestoreSpellSlots(List<SpellSlotEntry> spellSlots)
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || spellSlots == null)
            return;

        for (int i = 0; i < 4; i++)
            player.UnequipSpell(i);

        Dictionary<string, SpellDefinition> spellLookup = BuildSpellLookup();

        foreach (SpellSlotEntry slot in spellSlots)
        {
            if (slot == null)
                continue;
            if (slot.slotIndex < 0 || slot.slotIndex >= 4)
                continue;

            SpellDefinition spell = null;
            if (!string.IsNullOrWhiteSpace(slot.spellName))
                spellLookup.TryGetValue(slot.spellName, out spell);

            if (spell == null && slot.recipe >= 0 && Enum.IsDefined(typeof(Recipe), slot.recipe))
            {
                Recipe recipe = (Recipe)slot.recipe;
                foreach (SpellDefinition candidate in spellLookup.Values)
                {
                    if (candidate != null
                        && candidate.requiredRecipe == recipe)
                    {
                        spell = candidate;
                        break;
                    }
                }
            }

            if (spell != null)
                player.TryEquipSpell(spell, slot.slotIndex);
        }
    }

    private Dictionary<string, SpellDefinition> BuildSpellLookup()
    {
        Dictionary<string, SpellDefinition> lookup = new Dictionary<string, SpellDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (SpellDefinition spell in Resources.FindObjectsOfTypeAll<SpellDefinition>())
        {
            if (spell == null)
                continue;
            if (!lookup.ContainsKey(spell.name))
                lookup[spell.name] = spell;
        }

        PlayerController player = PlayerController.Instance;
        if (player != null)
        {
            SpellDefinition[] loadout = player.GetLoadout();
            if (loadout != null)
            {
                foreach (SpellDefinition spell in loadout)
                {
                    if (spell == null)
                        continue;
                    if (!lookup.ContainsKey(spell.name))
                        lookup[spell.name] = spell;
                }
            }
        }

        return lookup;
    }
}