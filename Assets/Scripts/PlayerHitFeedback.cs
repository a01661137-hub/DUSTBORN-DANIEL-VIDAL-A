using UnityEngine;

/// <summary>
/// Reacción cuando el jugador es golpeado:
/// - La cámara tiembla SIEMPRE que un ataque del boss te alcanza (aunque lo bloquees o lo esquives).
/// - El cuerpo parpadea en rojo y la pantalla se tiñe de rojo SOLO cuando recibes daño; el rojo se va quitando poco a poco.
/// Ponlo en ROACH (el mismo objeto que tiene Health).
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("Parpadeo del cuerpo")]
    [SerializeField] private Renderer bodyRenderer; // arrastra aquí el hijo "Cube"
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.25f;

    [Header("Temblor de cámara")]
    [SerializeField] private ThirdPersonCamera cameraShaker; // la Main Camera
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeMagnitude = 0.1f; // sube el valor para un temblor más fuerte

    [Header("Pantalla roja (solo cuando recibes daño)")]
    [SerializeField] private bool redScreen = true;
    [SerializeField] private Color redColor = new Color(0.85f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float redMaxAlpha = 0.55f; // qué tan roja se pone al principio
    [SerializeField, Min(0.1f)] private float redFadeDuration = 2.5f;  // segundos que tarda en quitarse

    private Health health;
    private Material material;
    private Color originalColor;
    private Texture2D vignette;
    private float flashUntil;
    private float redStartTime = -999f;
    private float lastShakeTime = -999f;
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

        vignette = CreateVignette();
    }

    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable() => health.Damaged -= OnDamaged;

    /// <summary>
    /// El boss avisa aquí cada vez que su ataque te alcanza, aunque el escudo o la esquiva
    /// evite el daño. Solo hace temblar la cámara.
    /// </summary>
    public void NotifyBossHit()
    {
        Shake();
    }

    private void OnDamaged(float amount)
    {
        flashUntil = Time.time + flashDuration;
        redStartTime = Time.time;

        // Si el boss ya hizo temblar la cámara en este mismo golpe, no se repite.
        if (Time.time - lastShakeTime > 0.1f) Shake();
    }

    private void Shake()
    {
        lastShakeTime = Time.time;
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

    // El rojo empieza fuerte y se va quitando poco a poco.
    private void OnGUI()
    {
        if (!redScreen || Event.current.type != EventType.Repaint) return;

        float t = (Time.time - redStartTime) / redFadeDuration;
        if (t >= 1f) return;

        float alpha = redMaxAlpha * Mathf.Pow(1f - Mathf.Clamp01(t), 1.5f);
        if (alpha <= 0.005f) return;

        GUI.depth = 10; // detrás del HUD (el HUD usa depth 0)
        GUI.color = new Color(redColor.r, redColor.g, redColor.b, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    // Textura con los bordes más rojos que el centro (efecto viñeta).
    private static Texture2D CreateVignette()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.2f);
                float alpha = 0.3f + 0.7f * d * d; // centro algo rojo, bordes muy rojos
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }
}