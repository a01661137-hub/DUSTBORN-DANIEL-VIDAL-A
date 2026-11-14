using UnityEngine;

/// <summary>
/// Cámara en tercera persona estilo soulslike: órbita libre con el mouse,
/// colisión con paredes y lock-on a enemigos (tecla Q o clic de la rueda).
/// Colócala en la Main Camera. NO la hagas hija del jugador.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Seguimiento")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float distance = 5f;
    [SerializeField] private float followSmoothTime = 0.08f;

    [Header("Cámara libre")]
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool invertY = false;

    [Header("Colisión")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minDistance = 0.8f;

    [Header("Lock-on")]
    [SerializeField] private KeyCode lockOnKey = KeyCode.Q;
    [SerializeField] private float lockOnRange = 20f;
    [SerializeField] private float lockOnBreakRange = 25f;
    [SerializeField] private float lockOnMaxAngle = 70f;
    [SerializeField] private float lockOnTurnSpeed = 8f;
    [SerializeField] private float lockOnPitch = 18f;
    [Tooltip("Cuánto baja el ángulo de la cámara según la altura del punto de mira del jefe. 0 = no lo usa, 1 = normal, más = mira más hacia arriba.")]
    [SerializeField] private float lockOnAimPitchWeight = 1f;
    [Tooltip("Metros extra que se aleja la cámara cuando está fijada (para que quepa un jefe grande).")]
    [SerializeField] private float lockOnExtraDistance = 1.5f;

    private float yaw;
    private float pitch;
    private Vector3 pivotPosition;
    private Vector3 pivotVelocity;
    private LockOnTarget lockedTarget;
    private float lockDistanceBlend;

    private float shakeTimeLeft;
    private float shakeTotal = 1f;
    private float shakeMagnitude;

    public bool IsLockedOn => lockedTarget != null;
    public Vector3 LockedAimPosition => lockedTarget != null ? lockedTarget.AimPosition : Vector3.zero;

    /// <summary>Sacude la cámara un momento (por ejemplo al recibir un golpe).</summary>
    public void Shake(float duration, float magnitude)
    {
        shakeTotal = Mathf.Max(duration, 0.01f);
        shakeTimeLeft = shakeTotal;
        shakeMagnitude = magnitude;
    }

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = Mathf.DeltaAngle(0f, angles.x);

        if (target != null)
            pivotPosition = target.position + pivotOffset;

        SetCursorLocked(true);
    }

    private void Update()
    {
        // Atajos de cursor para poder salir del juego en el editor.
        if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);

        HandleLockOnInput();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        pivotPosition = Vector3.SmoothDamp(
            pivotPosition, target.position + pivotOffset, ref pivotVelocity, followSmoothTime);

        if (lockedTarget != null)
            UpdateLockedRotation();
        else
            UpdateFreeRotation();

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = rotation * Vector3.back;

        // Con la cámara fijada se aleja un poco para que quepa un jefe grande.
        lockDistanceBlend = Mathf.MoveTowards(lockDistanceBlend, lockedTarget != null ? 1f : 0f, Time.deltaTime * 2f);
        float currentDistance = distance + lockOnExtraDistance * lockDistanceBlend;

        float finalDistance = currentDistance;
        if (Physics.SphereCast(pivotPosition, collisionRadius, direction, out RaycastHit hit,
                currentDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            finalDistance = Mathf.Max(minDistance, hit.distance);
        }

        transform.position = pivotPosition + direction * finalDistance;
        transform.rotation = rotation;

        // El temblor se suma al final de cada frame y se apaga solo.
        if (shakeTimeLeft > 0f)
        {
            shakeTimeLeft -= Time.deltaTime;
            float falloff = Mathf.Clamp01(shakeTimeLeft / shakeTotal);
            transform.position += Random.insideUnitSphere * shakeMagnitude * falloff;
        }
    }

    private void UpdateFreeRotation()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? -1f : 1f);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateLockedRotation()
    {
        Vector3 aim = lockedTarget.AimPosition;
        Vector3 dir = aim - target.position;
        float aimHeight = aim.y - pivotPosition.y; // qué tan arriba del pivote está el punto de mira
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
        {
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, targetYaw, lockOnTurnSpeed * Time.deltaTime);
        }

        // Si el punto de mira está alto (la cabeza de un jefe grande), la cámara baja el ángulo para encuadrarlo.
        float targetPitch = lockOnPitch;
        float horizontal = Mathf.Max(dir.magnitude, 1f);
        targetPitch -= Mathf.Atan2(aimHeight, horizontal) * Mathf.Rad2Deg * lockOnAimPitchWeight;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        pitch = Mathf.Lerp(pitch, targetPitch, lockOnTurnSpeed * Time.deltaTime);
    }

    private void HandleLockOnInput()
    {
        if (target == null) return;

        if (Input.GetKeyDown(lockOnKey) || Input.GetKeyDown(KeyCode.Mouse2))
            lockedTarget = lockedTarget != null ? null : FindBestTarget();

        // Soltar el lock-on si el enemigo murió, se desactivó o se alejó demasiado.
        if (lockedTarget != null)
        {
            bool gone = !lockedTarget.isActiveAndEnabled;
            bool tooFar = Vector3.Distance(target.position, lockedTarget.transform.position) > lockOnBreakRange;
            if (gone || tooFar) lockedTarget = null;
        }
    }

    // Elige el enemigo más cercano al centro de la pantalla dentro del rango.
    private LockOnTarget FindBestTarget()
    {
        LockOnTarget best = null;
        float bestAngle = lockOnMaxAngle;

        Vector3 camForward = transform.forward;
        camForward.y = 0f;

        foreach (LockOnTarget candidate in LockOnTarget.All)
        {
            Vector3 toTarget = candidate.AimPosition - target.position;
            if (toTarget.magnitude > lockOnRange) continue;

            toTarget.y = 0f;
            float angle = Vector3.Angle(camForward, toTarget);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = candidate;
            }
        }

        return best;
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}