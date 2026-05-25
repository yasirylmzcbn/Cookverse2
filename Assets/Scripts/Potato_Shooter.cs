using UnityEngine;
using System.Collections;
using System.Resources;

public class Potato_Shooter : MonoBehaviour
{

    public GameObject Bullet;
    public Transform Shoot_Pos;
    public int maxAmmo;
    public float shootCooldownDuration;
    public float reloadDuration = 2f;
    public int currentAmmo;
    private Coroutine reloadCoroutine;
    private Coroutine shootCooldownCoroutine;
    enum WeaponState
    {
        Ready,
        Cooldown, //async op to wait for the cooldown and then set it to ready or empty
        Reloading,
        Empty
    }
    private WeaponState state;
    [Header("Aiming")]
    [Tooltip("Optional override. If empty, uses the currently active camera from SwitchCamera, else Camera.main.")]
    [SerializeField] private Transform aimReference;

    [Tooltip("If true, aim is computed by raycasting from the camera forward and shooting towards the hit point. Helps in third-person.")]
    [SerializeField] private bool useCameraRaycastAim = true;

    [SerializeField] private float aimMaxDistance = 200f;

    [Tooltip("Layers the aim ray can hit.")]
    [SerializeField] private LayerMask aimLayers = ~0;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSfx;
    [Range(0f, 1f)]
    [SerializeField] private float fireSfxVolume = 1f;

    [SerializeField] private AudioClip reloadSfx;
    [Range(0f, 1f)]
    [SerializeField] private float reloadSfxVolume = 1f;

    private SwitchCamera _switchCamera;
    private AudioSource _audioSource;
    private Quaternion _initialLocalRotation;
    private Coroutine _recoilCoroutine;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        _switchCamera = FindFirstObjectByType<SwitchCamera>();
        currentAmmo = maxAmmo;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        
        _initialLocalRotation = transform.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void Shoot()
    {
        if (state == WeaponState.Cooldown || state == WeaponState.Reloading)
        {
            return;
        }
        if (currentAmmo <= 0)
        {
            state = WeaponState.Empty;
            //make a click sound or change color in grayboxing
            return;
        }
        if (Bullet == null || Shoot_Pos == null) return;

        Transform aim = GetAimTransform();
        Vector3 direction = GetAimDirection(aim);

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Instantiate(Bullet, Shoot_Pos.position, rotation);
        if (fireSfx != null)
            AudioSource.PlayClipAtPoint(fireSfx, Shoot_Pos.position, fireSfxVolume);
        currentAmmo -= 1;
        
        if (_recoilCoroutine != null)
            StopCoroutine(_recoilCoroutine);
        _recoilCoroutine = StartCoroutine(RecoilRoutine());

        SetCooldown();
    }

    public void TryReload()
    {
        if (currentAmmo >= maxAmmo)
        {
            return;
        }

        // Already reloading, don't start another reload
        if (state == WeaponState.Reloading)
        {
            return;
        }

        // Stop any active cooldown coroutine to allow reload
        if (shootCooldownCoroutine != null)
        {
            StopCoroutine(shootCooldownCoroutine);
            shootCooldownCoroutine = null;
        }

        // Stop any stale reload coroutine
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        if (reloadSfx != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(reloadSfx, reloadSfxVolume);
        }

        reloadCoroutine = StartCoroutine(ReloadRoutine());
        state = WeaponState.Reloading;
    }

    IEnumerator ReloadRoutine()
    {
        //animation
        yield return new WaitForSeconds(reloadDuration);
        currentAmmo = maxAmmo;
        state = WeaponState.Ready;
        reloadCoroutine = null;
    }

    public void SetCooldown()
    {
        if (state == WeaponState.Cooldown || shootCooldownCoroutine != null || state == WeaponState.Reloading)
        {
            return;
        }

        shootCooldownCoroutine = StartCoroutine(WaitCooldownRoutine());
        state = WeaponState.Cooldown;
    }

    // for respawning
    public void ResetAmmo()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }

        if (shootCooldownCoroutine != null)
        {
            StopCoroutine(shootCooldownCoroutine);
            shootCooldownCoroutine = null;
        }

        currentAmmo = maxAmmo;
        state = WeaponState.Ready;
    }


    public bool IsReloading => state == WeaponState.Reloading; // for UI

    IEnumerator WaitCooldownRoutine()
    {
        yield return new WaitForSeconds(shootCooldownDuration);
        if (state == WeaponState.Cooldown) //so that shooting during reload can't happen
        {
            if (currentAmmo <= 0)
            {
                state = WeaponState.Empty;
                TryReload();
            }
            else
            {
                state = WeaponState.Ready;
            }
        }
        shootCooldownCoroutine = null;
    }

    private IEnumerator RecoilRoutine()
    {
        float elapsed = 0f;
        // Make the recoil finish relatively quickly but safely inside the cooldown period 
        float duration = Mathf.Min(0.15f, shootCooldownDuration > 0 ? shootCooldownDuration * 0.8f : 0.1f);
        float halfDuration = duration / 2f;
        
        // Random pitch (up/down) between -1 and 1 degrees
        float randomPitch = Random.Range(-1f, 1f);
        Quaternion targetRecoil = _initialLocalRotation * Quaternion.Euler(randomPitch, 0f, 0f);

        // Animate towards the recoil angle
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(_initialLocalRotation, targetRecoil, elapsed / halfDuration);
            yield return null;
        }

        // Animate back to original
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(targetRecoil, _initialLocalRotation, elapsed / halfDuration);
            yield return null;
        }

        transform.localRotation = _initialLocalRotation;
        _recoilCoroutine = null;
    }

    private Transform GetAimTransform()
    {
        if (aimReference != null)
            return aimReference;

        if (_switchCamera == null)
            _switchCamera = FindFirstObjectByType<SwitchCamera>();

        if (_switchCamera != null)
        {
            if (_switchCamera.kitchenCamera != null && _switchCamera.kitchenCamera.activeInHierarchy)
                return _switchCamera.kitchenCamera.transform;

            if (_switchCamera.thirdPersonCamera != null && _switchCamera.thirdPersonCamera.activeInHierarchy)
                return _switchCamera.thirdPersonCamera.transform;

            if (_switchCamera.firstPersonCamera != null && _switchCamera.firstPersonCamera.activeInHierarchy)
                return _switchCamera.firstPersonCamera.transform;
        }

        return Camera.main != null ? Camera.main.transform : transform;
    }

    private Vector3 GetAimDirection(Transform aim)
    {
        if (!useCameraRaycastAim || aim == null)
            return aim != null ? aim.forward : transform.forward;

        Ray ray = new Ray(aim.position, aim.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, aimLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 toHit = hit.point - Shoot_Pos.position;
            return toHit.sqrMagnitude > 0.0001f ? toHit.normalized : aim.forward;
        }

        //return aim.forward;
        Vector3 farPoint = aim.position + aim.forward * aimMaxDistance;
        return (farPoint - Shoot_Pos.position).normalized;
    }
}
