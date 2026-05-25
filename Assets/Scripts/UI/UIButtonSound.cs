using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable == null || !selectable.interactable)
            return;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUIHoverSound();
            return;
        }

        if (UISoundManager.Instance != null)
        {
            UISoundManager.Instance.PlayHoverSound();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (selectable == null || !selectable.interactable)
            return;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUIClickSound();
            return;
        }

        if (UISoundManager.Instance != null)
        {
            UISoundManager.Instance.PlayClickSound();
        }
    }
}
