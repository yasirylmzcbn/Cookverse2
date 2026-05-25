using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KitchenController : MonoBehaviour, IInteractable
{
    [SerializeField] private SwitchCamera switchCamera;

    bool IInteractable.Interact()
    {
        if (switchCamera == null)
            switchCamera = FindFirstObjectByType<SwitchCamera>();

        if (switchCamera != null)
        {
            return switchCamera.SwitchToKitchenCamera(SwitchCamera.KitchenCameras.Stove);
        }
        return false;
    }

    private void OnGUI()
    {
        if (Time.timeScale == 0f) return;

        if (PlayerController.Instance == null || PlayerController.Instance.InteractorSource == null) return;

        if (switchCamera == null)
            switchCamera = FindFirstObjectByType<SwitchCamera>();

        if (switchCamera != null && switchCamera.IsInKitchenCamera) return;

        bool canInteract = false;
        Ray r = new Ray(PlayerController.Instance.InteractorSource.position, PlayerController.Instance.InteractorSource.forward);
        if (Physics.Raycast(r, out RaycastHit hit, PlayerController.Instance.InteractDistance))
        {
            IInteractable interactable = null;
            hit.collider.gameObject.TryGetComponent(out interactable);
            
            if (interactable == null)
                interactable = hit.collider.GetComponentInParent<IInteractable>();
                
            if (interactable == null && hit.rigidbody != null)
                interactable = hit.rigidbody.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable as Component != null && (interactable as Component).gameObject == gameObject)
            {
                canInteract = true;
            }
        }

        if (canInteract)
        {
            PauseScript pauseScript = PauseScript.Instance != null ? PauseScript.Instance : FindFirstObjectByType<PauseScript>();
            float promptFontSizeScale = pauseScript != null ? pauseScript.PromptFontSizeScale : 1f;
            float promptVerticalPositionScale = pauseScript != null ? pauseScript.PromptVerticalPositionScale : 1f;

            Color originalColor = GUI.color;
            GUI.color = Color.white;
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(36f * promptFontSizeScale)
            };

            GUI.Label(new Rect(0, Screen.height * 0.7f * promptVerticalPositionScale, Screen.width, 100f), "Press E to cook", style);
            GUI.color = originalColor;
        }
    }
}
