using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialSlideshow", menuName = "Cookverse/UI/Tutorial Slideshow")]
public class TutorialSlideshow : ScriptableObject
{
    [TextArea]
    [SerializeField] private string description;
    [Tooltip("Assign PNG textures here (recommended import type: Default).")]
    [SerializeField] private List<Texture2D> slides = new List<Texture2D>();

    public int SlideCount => slides != null ? slides.Count : 0;

    public Texture2D GetSlide(int index)
    {
        if (slides == null || index < 0 || index >= slides.Count)
            return null;

        return slides[index];
    }
}
