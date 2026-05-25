using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class EquippedDiamondUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    , IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("0=Up, 1=Right, 2=Down, 3=Left")]
    public int slotIndex;

    [SerializeField] private Image iconImage;
    [SerializeField] private Image diamondBackground;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Color emptyColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
    [SerializeField] private Color filledColor = new Color(0.3f, 0.6f, 0.9f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.9f, 0.7f, 0.1f, 1f);

    private SpellMenuUI _menu;
    private static GameObject _dragGhost;
    private Canvas _rootCanvas;

    private Canvas RootCanvas
    {
        get
        {
            if (_rootCanvas == null)
                _rootCanvas = GetComponentInParent<Canvas>();
            return _rootCanvas;
        }
    }

    public void Init(SpellMenuUI menu)
    {
        _menu = menu;
    }

    public void Refresh(SpellDefinition spell, bool isSelected)
    {
        bool filled = spell != null;
        bool showIcon = filled && spell.icon != null;
        if (diamondBackground != null)
            diamondBackground.color = isSelected ? selectedColor : (filled ? filledColor : emptyColor);
        if (iconImage != null)
        {
            if (iconImage.gameObject.activeSelf != showIcon)
                iconImage.gameObject.SetActive(showIcon);

            iconImage.enabled = showIcon;

            if (showIcon)
                iconImage.sprite = spell.icon;
        }
        if (slotLabel != null)
            slotLabel.text = filled ? spell.displayName : GetDirectionLabel(slotIndex);
    }

    public void OnPointerClick(PointerEventData e)
    {
        Debug.Log($"Diamond slot {slotIndex} clicked");
        _menu.OnDiamondClicked(slotIndex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUIHoverSound();
        else if (UISoundManager.Instance != null)
            UISoundManager.Instance.PlayHoverSound();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null || _menu == null) return;

        SpellDragDropItem payload = eventData.pointerDrag.GetComponent<SpellDragDropItem>();
        if (payload == null || payload.spell == null) return;

        if (payload.sourceDiamondSlot >= 0)
        {
            if (payload.sourceDiamondSlot != slotIndex)
                _menu.SwapEquippedSlots(payload.sourceDiamondSlot, slotIndex);
            return;
        }

        _menu.AssignSpellToSlot(payload.spell, slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_menu == null || RootCanvas == null) return;

        SpellDefinition equippedSpell = _menu.GetEquippedSpell(slotIndex);
        if (equippedSpell == null) return;

        if (_dragGhost != null) Destroy(_dragGhost);

        _dragGhost = new GameObject("EquippedSpellDragGhost");
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
        payload.spell = equippedSpell;
        payload.sourceDiamondSlot = slotIndex;

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

    private string GetDirectionLabel(int idx) => idx switch
    {
        0 => "↑",
        1 => "→",
        2 => "↓",
        3 => "←",
        _ => "?"
    };
}