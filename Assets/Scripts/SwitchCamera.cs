using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections.Generic;

public class SwitchCamera : MonoBehaviour
{
    private static SwitchCamera _instance;

    public GameObject firstPersonCamera;
    public GameObject thirdPersonCamera;
    public GameObject kitchenCamera;
    public enum KitchenCameras { Stove, Oven, Sink, Fryer, Microwave, }
    private Dictionary<KitchenCameras, GameObject> cameras;
    public KitchenCameras currentKitchenCamera;
    bool kitchenCam = false;
    bool firstPerson = true;
    [SerializeField] private GameObject hotbarUIPanel;

    private static bool IsAlive(GameObject go) => go != null;

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (IsAlive(go))
            go.SetActive(active);
    }

    public bool IsInKitchenCamera => kitchenCam;
    public bool IsInkitchenCamera => kitchenCam && currentKitchenCamera == KitchenCameras.Stove;

    public static bool IsKitchenInteractionAllowed()
    {
        if (_instance == null)
            _instance = FindFirstObjectByType<SwitchCamera>();

        return _instance != null
               && _instance.kitchenCam
               && Cursor.visible
               && Cursor.lockState == CursorLockMode.None;
    }

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StartCoroutine(DelayedEnsureAudioListener());
    }

    private System.Collections.IEnumerator DelayedEnsureAudioListener()
    {
        // Wait two full frames to ensure all duplicate "doomed" Player/Camera objects 
        // have been safely finalized and Destroyed by their respective loaders.
        yield return null;
        yield return null;
        EnsureActiveAudioListener();
    }

    void Start()
    {
        SetActiveSafe(firstPersonCamera, true);
        SetActiveSafe(thirdPersonCamera, false);
        SetActiveSafe(kitchenCamera, false);
        currentKitchenCamera = KitchenCameras.Stove;
        cameras = new Dictionary<KitchenCameras, GameObject>()
        {
            { KitchenCameras.Stove, kitchenCamera },
            // TODO Add other kitchen cameras here when implemented
        };

        StartCoroutine(DelayedEnsureAudioListener());
    }

    private bool TryGetKitchenCamera(KitchenCameras cam, out GameObject target)
    {
        target = null;

        if (cameras == null)
        {
            cameras = new Dictionary<KitchenCameras, GameObject>()
            {
                { KitchenCameras.Stove, kitchenCamera },
            };
        }

        if (!cameras.TryGetValue(cam, out target) || !IsAlive(target))
        {
            // Keep Stove in sync in case scene references changed.
            if (cam == KitchenCameras.Stove && IsAlive(kitchenCamera))
            {
                cameras[KitchenCameras.Stove] = kitchenCamera;
                target = kitchenCamera;
            }
        }

        return IsAlive(target);
    }

    void Update()
    {
        // if (Keyboard.current.vKey.wasPressedThisFrame)
        // {
        //     CycleCameraMode();
        // }
    }

    public void CycleCameraMode()
    {
        if (!kitchenCam)
        {
            if (firstPerson)
            {
                SetActiveSafe(firstPersonCamera, false);
                SetActiveSafe(thirdPersonCamera, true);
                firstPerson = false;
            }
            else
            {
                SetActiveSafe(thirdPersonCamera, false);
                SetActiveSafe(firstPersonCamera, true);
                firstPerson = true;
            }

            EnsureActiveAudioListener();
        }
    }

    public bool SwitchToKitchenCamera(KitchenCameras cam)
    {
        if (!TryGetKitchenCamera(cam, out GameObject targetKitchenCam))
            return false;

        if (PlayerController.Instance != null)
            PlayerController.Instance.SetBodyVisible(false);

        kitchenCam = true;
        SetActiveSafe(firstPersonCamera, false);
        SetActiveSafe(thirdPersonCamera, false);
        SetActiveSafe(targetKitchenCam, true);
        currentKitchenCamera = cam;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnsureActiveAudioListener();

        return true;
    }

    public void ExitKitchenCamera()
    {
        Debug.Log("Exiting kitchen camera");

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.SetBodyVisible(true);
            PlayerController.Instance.EndInteraction();
        }

        kitchenCam = false;
        SetActiveSafe(kitchenCamera, false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SpellMenuUI spellMenu = FindFirstObjectByType<SpellMenuUI>(FindObjectsInactive.Include);
        if (spellMenu != null && spellMenu.menuOpen)
            spellMenu.SetMenuVisible(false);

        if (firstPerson)
        {
            SetActiveSafe(firstPersonCamera, true);
            EnableFirstPersonLook();
        }
        else
        {
            SetActiveSafe(thirdPersonCamera, true);
        }

        CameraMove tpController = thirdPersonCamera != null
            ? thirdPersonCamera.GetComponentInChildren<CameraMove>(true)
            : null;

        if (tpController != null)
            tpController.enabled = !firstPerson;

        EnsureActiveAudioListener();
    }

    private void EnableFirstPersonLook()
    {
        CameraController fpController = null;

        if (firstPersonCamera != null)
            fpController = firstPersonCamera.GetComponentInChildren<CameraController>(true);

        if (fpController == null && PlayerController.Instance != null)
            fpController = PlayerController.Instance.GetComponentInChildren<CameraController>(true);

        if (fpController == null)
            fpController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);

        if (fpController != null)
            fpController.enabled = true;
    }

    private void EnsureActiveAudioListener()
    {
        Camera activeCamera = GetActiveGameplayCamera();
        if (activeCamera == null)
            return;

        AudioListener[] allListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        AudioListener targetListener = activeCamera.GetComponent<AudioListener>();
        if (targetListener == null)
            targetListener = activeCamera.gameObject.AddComponent<AudioListener>();

        // We MUST force the listener on the active camera to be enabled, and ALL other listeners in the scene to be disabled.
        // A common issue when changing scenes is Unity leaving a generic scene "Main Camera" listener enabled alongside our character.
        foreach (AudioListener listener in allListeners)
        {
            if (listener != null)
                listener.enabled = (listener == targetListener);
        }

        // Also force Time scale to normal if previously stuck (safeguard)
        if (Time.timeScale == 0f && !IsPausedMenuOpen())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // Ensure AudioListener itself is unpaused, as jumping scenes can sometimes inherit a paused audio state
        AudioListener.pause = false;
    }

    private bool IsPausedMenuOpen()
    {
        PauseScript pauseScript = FindFirstObjectByType<PauseScript>(FindObjectsInactive.Exclude);
        if (pauseScript != null)
        {
            // Simple heuristic, if timeScale is supposed to be 1, we are not paused.
        }
        return false;
    }

    private Camera GetActiveGameplayCamera()
    {
        if (kitchenCam)
        {
            Camera kitchenCamComponent = kitchenCamera != null ? kitchenCamera.GetComponentInChildren<Camera>(true) : null;
            if (kitchenCamComponent != null)
                return kitchenCamComponent;
        }

        if (firstPerson)
        {
            Camera fpCamera = firstPersonCamera != null ? firstPersonCamera.GetComponentInChildren<Camera>(true) : null;
            if (fpCamera != null)
                return fpCamera;
        }
        else
        {
            Camera tpCamera = thirdPersonCamera != null ? thirdPersonCamera.GetComponentInChildren<Camera>(true) : null;
            if (tpCamera != null)
                return tpCamera;
        }

        return Camera.main;
    }
}
