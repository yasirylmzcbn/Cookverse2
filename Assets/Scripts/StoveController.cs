using UnityEngine;

public class StoveScript : MonoBehaviour
{

    [Header("Burner Renderers (Optional)")]
    [SerializeField] private Renderer leftBurnerRenderer;
    [SerializeField] private Renderer rightBurnerRenderer;
    [SerializeField] private Color burnerOffColor = Color.black;
    [SerializeField] private Color burnerOnColor = Color.red;
    [Tooltip("Material color property name. '_BaseColor' (URP/HDRP) or '_Color' (Built-in).")]
    [SerializeField] private string colorProperty = "_BaseColor";

    SwitchCamera switchCamera;
    void Start()
    {
        switchCamera = FindFirstObjectByType<SwitchCamera>();
        ApplyVisuals(KnobController.BurnerSide.Left);
        ApplyVisuals(KnobController.BurnerSide.Right);
    }

    public void ApplyVisuals(KnobController.BurnerSide side, float heatLevel = 0f)
    {
        if (side == KnobController.BurnerSide.Left)
        {
            ApplyBurnerColor(leftBurnerRenderer, heatLevel);
        }
        else if (side == KnobController.BurnerSide.Right)
        {
            ApplyBurnerColor(rightBurnerRenderer, heatLevel);
        }
    }

    private void ApplyBurnerColor(Renderer rend, float level01)
    {
        if (rend == null) return;

        Color targetColor = Color.Lerp(burnerOffColor, burnerOnColor, Mathf.Clamp01(level01));
        rend.material.SetColor(colorProperty, targetColor);
    }
}
