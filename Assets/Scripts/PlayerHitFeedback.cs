using UnityEngine;

/// <summary>
/// Reacción cuando el jugador recibe daño: el cuerpo parpadea en rojo y la cámara tiembla un poco.
/// Ponlo en ROACH (el mismo objeto que tiene Health).
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("Parpadeo")]
    [SerializeField] private Renderer bodyRenderer; // arrastra aquí el hijo "Cube"
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.25f;

    [Header("Temblor de cámara")]
    [SerializeField] private ThirdPersonCamera cameraShaker; // la Main Camera
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeMagnitude = 0.1f; // sube el valor para un temblor más fuerte

    private Health health;
    private Material material;
    private Color originalColor;
    private float flashUntil;
    private bool flashing;

    private void Awake()
    {
        health = GetComponent<Health>();

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null)
        {
            material = bodyRenderer.material; // instancia propia
            originalColor = material.color;
        }

        if (cameraShaker == null && Camera.main != null)
            cameraShaker = Camera.main.GetComponent<ThirdPersonCamera>();
    }

    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable() => health.Damaged -= OnDamaged;

    private void OnDamaged(float amount)
    {
        flashUntil = Time.time + flashDuration;
        if (cameraShaker != null) cameraShaker.Shake(shakeDuration, shakeMagnitude);
    }

    // En LateUpdate para que el rojo gane sobre otros cambios de color (como el verde de curarse).
    private void LateUpdate()
    {
        if (material == null) return;

        if (Time.time < flashUntil)
        {
            material.color = hitColor;
            flashing = true;
        }
        else if (flashing)
        {
            material.color = originalColor;
            flashing = false;
        }
    }
}
