using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SpellUIScript : MonoBehaviour
{
    [Header("References")]
    public PlayerController playerController;

    [Header("Spell UI Images")]
    public Image spell1Image;
    public Image spell2Image;
    public Image spell3Image;
    public Image spell4Image;

    [Header("Transparency Settings")]
    [Range(0f, 1f)]
    public float transparencyAmount = 0.5f;

    private readonly Dictionary<Image, Color> _originalColors = new Dictionary<Image, Color>();
    private readonly SpellDefinition[] _lastSpells = new SpellDefinition[4];

    void Start()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }
        CacheOriginalColor(spell1Image);
        CacheOriginalColor(spell2Image);
        CacheOriginalColor(spell3Image);
        CacheOriginalColor(spell4Image);

        TryAssignIcon(spell1Image, 0);
        TryAssignIcon(spell2Image, 1);
        TryAssignIcon(spell3Image, 2);
        TryAssignIcon(spell4Image, 3);
    }

    void Update()
    {
        RefreshIconIfChanged(spell1Image, 0);
        RefreshIconIfChanged(spell2Image, 1);
        RefreshIconIfChanged(spell3Image, 2);
        RefreshIconIfChanged(spell4Image, 3);

        UpdateSpellTransparency(spell1Image, 0);
        UpdateSpellTransparency(spell2Image, 1);
        UpdateSpellTransparency(spell3Image, 2);
        UpdateSpellTransparency(spell4Image, 3);
    }

    private void RefreshIconIfChanged(Image spellImage, int slotIndex)
    {
        if (spellImage == null) return;
        if (playerController == null) return;

        SpellDefinition current = playerController.GetSpell(slotIndex);
        if (_lastSpells[slotIndex] == current) return;

        _lastSpells[slotIndex] = current;
        if (current != null && current.icon != null)
            spellImage.sprite = current.icon;
    }

    private void CacheOriginalColor(Image spellImage)
    {
        if (spellImage == null) return;
        if (_originalColors.ContainsKey(spellImage)) return;
        _originalColors[spellImage] = spellImage.color;
    }

    private void TryAssignIcon(Image spellImage, int slotIndex)
    {
        if (spellImage == null) return;
        if (playerController == null) return;

        SpellDefinition spell = playerController.GetSpell(slotIndex);
        if (spell != null && spell.icon != null)
            spellImage.sprite = spell.icon;
    }

    private void UpdateSpellTransparency(Image spellImage, int slotIndex)
    {
        if (spellImage == null) return;
        if (playerController == null) return;

        if (!_originalColors.TryGetValue(spellImage, out Color baseColor))
        {
            baseColor = spellImage.color;
            _originalColors[spellImage] = baseColor;
        }

        bool isUnlocked = playerController.IsSpellUnlocked(slotIndex);
        bool isOnCooldown = playerController.IsSpellOnCooldown(slotIndex);

        if (!isUnlocked)
        {
            Color lockedColor = baseColor;
            lockedColor.a = transparencyAmount;
            spellImage.color = lockedColor;
            return;
        }

        if (isOnCooldown)
        {
            // spell on cooldown, so transparent
            Color transparentColor = baseColor;
            transparentColor.a = transparencyAmount;
            spellImage.color = transparentColor;
        }
        else
        {
            // spell is ready, so opaque
            Color readyColor = baseColor;
            readyColor.a = 1f;
            spellImage.color = readyColor;
        }
    }
}