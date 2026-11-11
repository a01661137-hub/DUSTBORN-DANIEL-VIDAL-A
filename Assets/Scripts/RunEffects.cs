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

    [Header("Acercamiento de cámara al correr")]
    [SerializeField] private bool fovEnabled = true;
    [SerializeField] private Camera targetCamera;             // si lo dejas vacío usa la Main Camera
    [SerializeField] private float runFovBoost = 6f;          // grados extra de FOV al correr
    [SerializeField] private float fovSmoothSpeed = 4f;

    private CharacterController controller;
    private Vector3 lastPosition;
    private float nextDustTime;
    private float baseFov;
    private bool fovReady;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
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

        bool running = speed >= runSpeedThreshold && controller.isGrounded;

        if (dustEnabled && running && Time.time >= nextDustTime)
        {
            nextDustTime = Time.time + dustInterval;
            EmitDust();
        }

        if (fovEnabled && fovReady && targetCamera != null)
        {
            float targetFov = baseFov + (running ? runFovBoost : 0f);
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFov, fovSmoothSpeed * dt);
        }
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
