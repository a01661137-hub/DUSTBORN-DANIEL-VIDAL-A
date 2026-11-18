using UnityEngine;

/// <summary>
/// Efectos al correr: nubecitas de polvo en los pies y un ligero acercamiento de cámara (FOV).
/// Ponlo en ROACH (el objeto raíz con PlayerController y CharacterController).
/// Usa ImpactDust, así que no necesita prefabs ni texturas.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RunEffects : MonoBehaviour
{
    [Header("Cuándo cuenta como 'corriendo'")]
    [SerializeField] private float runSpeedThreshold = 4.5f; // metros/seg. Entre tu Walk (3) y tu Run (6)

    [Header("Polvo en los pies")]
    [SerializeField] private bool dustEnabled = true;
    [SerializeField] private float dustInterval = 0.18f;     // segundos entre nubecitas
    [SerializeField] private float dustBehindDistance = 0.4f; // qué tan atrás de los pies sale
    [SerializeField] private float dustSpread = 0.5f;
    [SerializeField] private float dustPuffSize = 0.8f;
    [SerializeField] private int dustCount = 3;
    [SerializeField] private Color dustColor = new Color(0.55f, 0.5f, 0.42f, 0.5f);
    [SerializeField] private float feetHeightAdjust = 0.05f;  // sube o baja el polvo si sale flotando o hundido

    [Header("Polvo al deslizarte (roll de barrido)")]
    [SerializeField] private bool slideDustEnabled = true;
    [Tooltip("Segundos después de empezar el deslizado en que empieza a salir polvo (los pies tocan el suelo).")]
    [SerializeField] private float slideDustStartDelay = 0.2f;
    [SerializeField] private float slideDustInterval = 0.05f;   // segundos entre nubes (menos = más polvo)
    [SerializeField] private float slideDustBehindDistance = 0.3f;
    [Tooltip("Apertura del abanico en grados (más grande = más abierto).")]
    [SerializeField] private float slideFanAngle = 70f;
    [Tooltip("Qué tan rápido sale el polvo hacia atrás (más = estela más larga).")]
    [SerializeField] private float slideDustSpeed = 4f;
    [SerializeField] private float slideDustPuffSize = 1.1f;
    [SerializeField] private int slideDustCount = 5;
    [SerializeField] private Color slideDustColor = new Color(0.6f, 0.55f, 0.46f, 0.6f);

    [Header("Acercamiento de cámara al correr")]
    [SerializeField] private bool fovEnabled = true;
    [SerializeField] private Camera targetCamera;             // si lo dejas vacío usa la Main Camera
    [SerializeField] private float runFovBoost = 6f;          // grados extra de FOV al correr
    [SerializeField] private float fovSmoothSpeed = 4f;
    [Tooltip("Grados extra de FOV mientras haces un roll corriendo (se suman a Run Fov Boost). 0 = sin cambio.")]
    [SerializeField] private float rollFovExtra = 6f;

    private CharacterController controller;
    private PlayerController player;
    private bool wasRunning;
    private Vector3 lastPosition;
    private float nextDustTime;
    private float nextSlideDustTime;
    private float slideStartTime;
    private bool wasSliding;
    private float baseFov;
    private bool fovReady;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        player = GetComponent<PlayerController>();
        lastPosition = transform.position;
    }

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null)
        {
            baseFov = targetCamera.fieldOfView;
            fovReady = true;
        }
    }

    private void OnDisable()
    {
        if (fovReady && targetCamera != null) targetCamera.fieldOfView = baseFov;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Velocidad horizontal real, medida por el cambio de posición.
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        float speed = delta.magnitude / dt;

        bool rolling = player != null && player.IsRolling;

        // Durante el roll se mantiene el estado en que venías (si corrías, el FOV se queda abierto).
        bool running = rolling ? wasRunning : (speed >= runSpeedThreshold && controller.isGrounded);
        wasRunning = running;

        if (dustEnabled && running && !rolling && Time.time >= nextDustTime)
        {
            nextDustTime = Time.time + dustInterval;
            EmitDust();
        }

        // Polvo mientras te deslizas (roll de barrido).
        bool sliding = rolling && player.CurrentRollIsAlt;
        if (sliding && !wasSliding) slideStartTime = Time.time;
        wasSliding = sliding;

        // El polvo empieza cuando los pies ya tocan el suelo, no cuando te avientas.
        if (slideDustEnabled && sliding
            && Time.time - slideStartTime >= slideDustStartDelay
            && Time.time >= nextSlideDustTime)
        {
            nextSlideDustTime = Time.time + slideDustInterval;
            EmitSlideDust();
        }

        if (fovEnabled && fovReady && targetCamera != null)
        {
            float targetFov = baseFov + (running ? runFovBoost : 0f) + (rolling && running ? rollFovExtra : 0f);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, fovSmoothSpeed * dt);
        }
    }

    private void EmitSlideDust()
    {
        float feetY = transform.position.y + controller.center.y - controller.height * 0.5f + feetHeightAdjust;
        Vector3 pos = transform.position - transform.forward * slideDustBehindDistance;
        pos.y = feetY;

        ImpactDust.FanBurst(pos, -transform.forward, slideFanAngle, slideDustSpeed,
            slideDustPuffSize, slideDustCount, slideDustColor);
    }

    private void EmitDust()
    {
        // Posición de los pies: la base de la cápsula del CharacterController.
        float feetY = transform.position.y + controller.center.y - controller.height * 0.5f + feetHeightAdjust;
        Vector3 pos = transform.position - transform.forward * dustBehindDistance;
        pos.y = feetY;

        ImpactDust.Burst(pos, dustSpread, dustPuffSize, dustCount, dustColor, dustColor, false);
    }
}