using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyUI : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraController firstPersonCameraController;

    [Header("Panels")]
    [SerializeField] private GameObject difficultyPanel;

    [Header("Buttons")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button mediumButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button bossButton;

    private bool storedVisible;
    private bool isPausedByGame;
    private CanvasGroup difficultyCanvasGroup;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        easyButton.onClick.AddListener(OnEasyButtonClicked);
        mediumButton.onClick.AddListener(OnMediumButtonClicked);
        hardButton.onClick.AddListener(OnHardButtonClicked);
        bossButton.onClick.AddListener(OnBossButtonClicked);

        SetMenuVisible(false);
        mediumButton.interactable = false;
        hardButton.interactable = false;
        bossButton.interactable = false;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
            return;

        if (storedVisible && Input.GetKeyDown(KeyCode.E))
        {
            SetMenuVisible(false);
            if (playerController != null)
                playerController.EndInteraction();
        }
    }

    public void SetMenuVisible(bool visible)
    {
        bool wasVisible = storedVisible;

        // Try to automatically find references if they are missing
        if (playerController == null)
            playerController = PlayerController.Instance != null ? PlayerController.Instance : FindFirstObjectByType<PlayerController>();

        if (firstPersonCameraController == null)
        {
            if (playerController != null)
                firstPersonCameraController = playerController.GetComponentInChildren<CameraController>(true);
            
            if (firstPersonCameraController == null)
                firstPersonCameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        }

        if (difficultyPanel != null)
            difficultyPanel.SetActive(visible);

        RefreshCanvasGroup();

        if (visible)
        {
            // Sync unlocks from GameManager
            if (GameManager.Instance != null)
            {
                UnlockDifficulty(GameManager.Instance.GetLastCompletedDifficulty());
            }

            // Disable player movement
            if (playerController != null)
                playerController.enabled = false;

            // Disable camera look
            if (firstPersonCameraController != null)
                firstPersonCameraController.enabled = false;

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Re-enable player movement
            if (playerController != null)
                playerController.enabled = true;

            // Re-enable camera
            if (firstPersonCameraController != null)
                firstPersonCameraController.enabled = true;

            // Lock cursor back
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Play close sound only on a real open -> closed transition.
            if (wasVisible && SoundManager.Instance != null)
                SoundManager.Instance.PlayUICloseSound();
        }
        storedVisible = visible;
        ApplyPauseState();
    }

    void OnEasyButtonClicked()
    {
        Debug.Log("Easy Clicked");

        SetMenuVisible(false);
        if (playerController != null)
            playerController.EndInteraction();
        GameManager.Instance.SetEasyDifficulty();
    }

    void OnMediumButtonClicked()
    {
        SetMenuVisible(false);
        if (playerController != null)
            playerController.EndInteraction();
        GameManager.Instance.SetMediumDifficulty();
    }

    void OnHardButtonClicked()
    {
        SetMenuVisible(false);
        if (playerController != null)
            playerController.EndInteraction();
        GameManager.Instance.SetHardDifficulty();
    }

    void OnBossButtonClicked()
    {
        SetMenuVisible(false);
        if (playerController != null)
            playerController.EndInteraction();
        GameManager.Instance.SetBossDifficulty();
    }

    public bool IsVisible()
    {
        return storedVisible;
    }

    public void SetPausedState(bool paused)
    {
        isPausedByGame = paused;
        ApplyPauseState();
    }

    public void ForceCloseForPause()
    {
        if (!storedVisible)
            return;

        storedVisible = false;

        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);
    }

    private void RefreshCanvasGroup()
    {
        if (difficultyPanel == null)
            return;

        if (difficultyCanvasGroup == null)
            difficultyCanvasGroup = difficultyPanel.GetComponent<CanvasGroup>();

        if (difficultyCanvasGroup == null)
            difficultyCanvasGroup = difficultyPanel.AddComponent<CanvasGroup>();
    }

    private void ApplyPauseState()
    {
        RefreshCanvasGroup();

        if (difficultyCanvasGroup == null)
            return;

        bool allowInteraction = storedVisible && !isPausedByGame;
        difficultyCanvasGroup.interactable = allowInteraction;
        difficultyCanvasGroup.blocksRaycasts = allowInteraction;
    }

    public void UnlockDifficulty(GameManager.Difficulty difficulty)
    {
        //Easy is unlocked on start
        if (difficulty >= GameManager.Difficulty.Easy)
        {
            mediumButton.interactable = true;
        }
        if (difficulty >= GameManager.Difficulty.Medium)
        {
            hardButton.interactable = true;
        }
        if (difficulty >= GameManager.Difficulty.Hard)
        {
            bossButton.interactable = true;
        }
    }
}
