using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SpellSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    , IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public SpellDefinition Spell { get; private set; }

    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI spellNameText;
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.3f, 0.9f);
    [SerializeField] private Color hoveredColor = new Color(0.4f, 0.4f, 0.6f, 1f);
    [SerializeField] private Color equippedColor = new Color(0.2f, 0.5f, 0.3f, 1f);

    private SpellMenuUI _menu;
    private static GameObject _dragGhost;
    private Canvas _rootCanvas;
    private bool _isHovered;
    private bool _isEquipped;

    private Canvas RootCanvas
    {
        get
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();
            return _rootCanvas;
        }
    }

    public void Init(SpellDefinition spell, SpellMenuUI menu)
    {
        EnsureImageWiring();
        EnsureNameWiring();
        Spell = spell;
        _menu = menu;
        if (iconImage != null && spell.icon != null)
            iconImage.sprite = spell.icon;
        _isHovered = false;
        _isEquipped = false;
        ApplyBackgroundColor();
        if (spellNameText != null)
            spellNameText.text = spell.displayName;
    }

    public void SetEquippedVisual(bool equipped)
    {
        _isEquipped = equipped;
        ApplyBackgroundColor();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUIHoverSound();
        else if (UISoundManager.Instance != null)
            UISoundManager.Instance.PlayHoverSound();

        _menu.OnSpellHovered(Spell);
        _isHovered = true;
        ApplyBackgroundColor();
    }

    public void OnPointerExit(PointerEventData e)
    {
        _menu.OnSpellHovered(null);
        _isHovered = false;
        SetEquippedVisual(_menu.IsEquipped(Spell));
    }

    public void OnPointerClick(PointerEventData e)
    {
        _menu.OnSpellClicked(Spell);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Spell == null || RootCanvas == null) return;

        if (_dragGhost != null) Destroy(_dragGhost);

        _dragGhost = new GameObject("SpellDragGhost");
        _dragGhost.transform.SetParent(RootCanvas.transform, false);
        _dragGhost.transform.SetAsLastSibling();

        Image ghostImage = _dragGhost.AddComponent<Image>();
        ghostImage.sprite = iconImage != null ? iconImage.sprite : null;
        ghostImage.color = new Color(1f, 1f, 1f, 0.75f);
        ghostImage.raycastTarget = false;

        RectTransform rt = _dragGhost.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60f, 60f);

        SpellDragDropItem payload = GetComponent<SpellDragDropItem>();
        if (payload == null) payload = gameObject.AddComponent<SpellDragDropItem>();
        payload.spell = Spell;
        payload.sourceDiamondSlot = -1;

        MoveDragGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveDragGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
        }
    }

    private void MoveDragGhost(PointerEventData eventData)
    {
        if (_dragGhost == null || RootCanvas == null) return;

        RectTransform canvasRT = RootCanvas.GetComponent<RectTransform>();
        Camera cam = RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventData.pressEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, cam, out Vector2 localPoint))
            _dragGhost.GetComponent<RectTransform>().localPosition = localPoint;
    }

    private void EnsureImageWiring()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (iconImage == null || iconImage == backgroundImage)
        {
            Image[] allImages = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < allImages.Length; i++)
            {
                Image candidate = allImages[i];
                if (candidate != null && candidate != backgroundImage)
                {
                    iconImage = candidate;
                    break;
                }
            }
        }

        // If this slot has a Button with Color Tint transition, it can override script colors.
        Button button = GetComponent<Button>();
        if (button != null && button.transition != Selectable.Transition.None)
            button.transition = Selectable.Transition.None;
    }

    private void EnsureNameWiring()
    {
        if (spellNameText != null)
            return;

        spellNameText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void ApplyBackgroundColor()
    {
        if (backgroundImage == null)
            return;

        if (_isHovered)
            backgroundImage.color = hoveredColor;
        else if (_isEquipped)
            backgroundImage.color = equippedColor;
        else
            backgroundImage.color = normalColor;
    }
}