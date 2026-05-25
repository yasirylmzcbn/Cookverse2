using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class SpellMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerRecipeUnlocks playerRecipeUnlocks;
    [SerializeField] private RecipeSpellDatabase recipeSpellDatabase;
    [SerializeField] private CameraController firstPersonCameraController;

    [Header("Panels")]
    [SerializeField] private GameObject spellMenuRoot;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject hotbarPanel;

    [Header("Description Panel (left 25%)")]
    [SerializeField] private TextMeshProUGUI spellNameText;
    [SerializeField] private TextMeshProUGUI spellDescText;
    [SerializeField] private Image spellPreviewIcon;

    [Header("Spell List Panel (left-mid 25%)")]
    [SerializeField] private Transform spellListContainer;  // Vertical Layout Group parent
    [SerializeField] private GameObject spellSlotPrefab;    // prefab with SpellSlotUI

    [Header("Equipped Diamond Slots (right 25%)")]
    [SerializeField] private EquippedDiamondUI[] diamondSlots = new EquippedDiamondUI[4];
    // Assign in Inspector: 0=Up, 1=Right, 2=Down, 3=Left

    public bool menuOpen;
    private int _selectedDiamondSlot = -1;  // which diamond the player is assigning TO
    private List<SpellSlotUI> _spellSlotUIs = new();

    private void Awake()
    {
        RebindReferences();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        RebindReferences();
        for (int i = 0; i < diamondSlots.Length; i++)
            diamondSlots[i]?.Init(this);

        SetMenuVisible(false);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindReferences();
    }

    private void RebindReferences()
    {
        if (PlayerController.Instance != null)
            playerController = PlayerController.Instance;
        else if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (PlayerRecipeUnlocks.Instance != null)
            playerRecipeUnlocks = PlayerRecipeUnlocks.Instance;
        else if (playerRecipeUnlocks == null)
            playerRecipeUnlocks = FindFirstObjectByType<PlayerRecipeUnlocks>();

        if (firstPersonCameraController == null)
        {
            if (playerController != null)
                firstPersonCameraController = playerController.GetComponentInChildren<CameraController>(true);

            if (firstPersonCameraController == null)
                firstPersonCameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        }
    }

    public void SetMenuVisible(bool visible)
    {
        RebindReferences();
        bool wasOpen = menuOpen;
        menuOpen = visible;
        if (spellMenuRoot != null)
            spellMenuRoot.SetActive(visible);
        if (inventoryPanel != null) inventoryPanel.SetActive(visible);
        // if (hotbarPanel != null) hotbarPanel.SetActive(visible);

        if (visible)
        {
            // lock looking around with cam, unlcok cursor
            if (firstPersonCameraController != null)
                firstPersonCameraController.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RebuildSpellList();
            RefreshDiamonds();
            ClearDescription();
            inventoryPanel.GetComponent<InventoryUI>().RefreshUI();

            // Play open sound only on a real closed -> open transition.
            if (!wasOpen && SoundManager.Instance != null)
                SoundManager.Instance.PlayUIOpenSound();
        }
        else
        {
            // If we're in kitchen interaction mode, keep the cursor available.
            SwitchCamera switchCamera = FindFirstObjectByType<SwitchCamera>();
            bool keepKitchenCursor = switchCamera != null && switchCamera.IsInKitchenCamera;

            if (firstPersonCameraController != null)
                firstPersonCameraController.enabled = true;

            Cursor.lockState = keepKitchenCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = keepKitchenCursor;
            _selectedDiamondSlot = -1;

            // Play close sound only on a real open -> closed transition.
            if (wasOpen && SoundManager.Instance != null)
                SoundManager.Instance.PlayUICloseSound();
        }
    }

    // ── Spell List ────────────────────────────────────────────

    private void RebuildSpellList()
    {
        RebindReferences();
        Debug.Log("Rebuilding spell list UI");
        foreach (var ui in _spellSlotUIs)
            if (ui != null) Destroy(ui.gameObject);
        _spellSlotUIs.Clear();

        if (recipeSpellDatabase == null) return;

        var entries = recipeSpellDatabase.GetEntries();
        Debug.Log("Entries: " + entries);
        foreach (var entry in entries)
        {
            Debug.Log("Checking spell entry: " + entry.spell + " requires recipe: " + entry.recipe);
            if (entry.spell == null) continue;
            Debug.Log("Is recipe unlocked?" + (playerRecipeUnlocks != null ? playerRecipeUnlocks.IsUnlocked(entry.recipe).ToString() : "No playerRecipeUnlocks reference"));
            if (playerRecipeUnlocks == null || !playerRecipeUnlocks.IsUnlocked(entry.recipe)) continue;

            var go = Instantiate(spellSlotPrefab, spellListContainer);
            var ui = go.GetComponent<SpellSlotUI>();
            ui.Init(entry.spell, this);
            ui.SetEquippedVisual(IsEquipped(entry.spell));
            _spellSlotUIs.Add(ui);
        }

        if (UISoundManager.Instance != null)
            UISoundManager.Instance.InjectSoundsIntoAllButtons();
    }

    // ── Diamond Slots ─────────────────────────────────────────

    public void OnDiamondClicked(int slotIndex)
    {
        Debug.Log($"Diamond slot {slotIndex} clicked in SpellMenuUI");
        // Toggle selection — clicking a diamond means "I want to assign a spell here"
        _selectedDiamondSlot = (_selectedDiamondSlot == slotIndex) ? -1 : slotIndex;
        RefreshDiamonds();
    }

    private void RefreshDiamonds()
    {
        RebindReferences();
        if (playerController == null) return;

        for (int i = 0; i < diamondSlots.Length; i++)
        {
            var spell = playerController.GetSpell(i);
            diamondSlots[i]?.Refresh(spell, i == _selectedDiamondSlot);
        }
        // Update equipped highlights on list
        foreach (var ui in _spellSlotUIs)
            ui.SetEquippedVisual(IsEquipped(ui.Spell));
    }

    // ── Spell Click (assign to selected diamond) ──────────────

    public void OnSpellClicked(SpellDefinition spell)
    {
        RebindReferences();
        if (playerController == null || spell == null) return;

        if (TryUnequipSpell(spell))
        {
            _selectedDiamondSlot = -1;
            RefreshDiamonds();
            RebuildSpellList();
            return;
        }

        if (_selectedDiamondSlot < 0)
        {
            if (!TryAutoEquipSpell(spell) && spellDescText != null)
                spellDescText.text = "All spell slots are full. Drag to replace or select a diamond slot first.";
            return;
        }

        AssignSpellToSlot(spell, _selectedDiamondSlot);
        _selectedDiamondSlot = -1;  // deselect after assigning
    }

    private bool TryUnequipSpell(SpellDefinition spell)
    {
        RebindReferences();
        if (playerController == null || spell == null)
            return false;

        var loadout = playerController.GetLoadout();
        if (loadout == null)
            return false;

        for (int i = 0; i < loadout.Length; i++)
        {
            if (loadout[i] == spell)
            {
                playerController.UnequipSpell(i);
                return true;
            }
        }

        return false;
    }

    // ── Hover description ─────────────────────────────────────

    public void OnSpellHovered(SpellDefinition spell)
    {
        if (spell == null) { ClearDescription(); return; }
        if (spellNameText != null) spellNameText.text = spell.displayName;
        if (spellDescText != null) spellDescText.text = spell.description;
        if (spellPreviewIcon != null)
        {
            spellPreviewIcon.enabled = spell.icon != null;
            if (spell.icon != null) spellPreviewIcon.sprite = spell.icon;
        }
    }

    private void ClearDescription()
    {
        if (spellNameText != null) spellNameText.text = "";
        if (spellDescText != null) spellDescText.text = "Hover a spell to see details";
        if (spellPreviewIcon != null) spellPreviewIcon.enabled = false;
    }

    // ── Helpers ───────────────────────────────────────────────

    public bool IsEquipped(SpellDefinition spell)
    {
        RebindReferences();
        if (playerController == null) return false;

        if (spell == null) return false;
        var loadout = playerController.GetLoadout();
        for (int i = 0; i < loadout.Length; i++)
            if (loadout[i] == spell) return true;
        return false;
    }

    public SpellDefinition GetEquippedSpell(int slotIndex)
    {
        RebindReferences();
        if (playerController == null) return null;
        return playerController.GetSpell(slotIndex);
    }

    public bool AssignSpellToSlot(SpellDefinition spell, int slotIndex)
    {
        RebindReferences();
        if (playerController == null || spell == null) return false;
        if (slotIndex < 0 || slotIndex >= diamondSlots.Length) return false;

        var loadout = playerController.GetLoadout();
        for (int i = 0; i < loadout.Length; i++)
        {
            if (i != slotIndex && loadout[i] == spell)
                playerController.UnequipSpell(i);
        }

        bool equipped = playerController.TryEquipSpell(spell, slotIndex);
        if (equipped)
            RefreshDiamonds();
        return equipped;
    }

    public bool TryAutoEquipSpell(SpellDefinition spell)
    {
        RebindReferences();
        if (playerController == null || spell == null) return false;

        for (int i = 0; i < diamondSlots.Length; i++)
        {
            if (playerController.GetSpell(i) == null)
                return AssignSpellToSlot(spell, i);
        }

        return false;
    }

    public bool SwapEquippedSlots(int sourceIndex, int targetIndex)
    {
        RebindReferences();
        if (playerController == null) return false;
        if (sourceIndex < 0 || sourceIndex >= diamondSlots.Length) return false;
        if (targetIndex < 0 || targetIndex >= diamondSlots.Length) return false;
        if (sourceIndex == targetIndex) return false;

        SpellDefinition sourceSpell = playerController.GetSpell(sourceIndex);
        SpellDefinition targetSpell = playerController.GetSpell(targetIndex);

        playerController.UnequipSpell(sourceIndex);
        playerController.UnequipSpell(targetIndex);

        if (sourceSpell != null)
            playerController.TryEquipSpell(sourceSpell, targetIndex);
        if (targetSpell != null)
            playerController.TryEquipSpell(targetSpell, sourceIndex);

        RefreshDiamonds();
        return true;
    }
}