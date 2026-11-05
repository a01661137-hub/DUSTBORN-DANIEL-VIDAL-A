using UnityEngine;

/// <summary>
/// Lluvia por código: gotas largas y finas que caen alrededor del jugador y siguen su movimiento,
/// con salpicaduras donde tocan el piso. Opcional: oscurece la luz, agrega neblina y sonido.
/// Ponlo en un objeto vacío (por ejemplo "Lluvia") y arrastra a ROACH en Follow Target.
/// </summary>
public class RainEffect : MonoBehaviour
{
    [Header("Seguir a")]
    [SerializeField] private Transform followTarget; // ROACH (si lo dejas vacío, sigue a la cámara)

    [Header("Lluvia")]
    [SerializeField, Range(0f, 1f)] private float intensity = 0.6f; // 0.3 = llovizna, 0.6 = moderada, 1 = tormenta
    [SerializeField] private int maxDropsPerSecond = 1400;          // gotas por segundo con intensidad 1
    [SerializeField] private float fadeInTime = 4f;                 // segundos que tarda en empezar a llover fuerte
    [SerializeField] private float areaSize = 50f;                  // ancho del área de lluvia alrededor del jugador
    [SerializeField] private float height = 20f;                    // desde qué altura caen
    [SerializeField] private float dropSpeed = 22f;
    [SerializeField] private float dropLength = 0.9f;               // largo de cada gota
    [SerializeField] private float dropWidth = 0.04f;               // grosor de cada gota
    [SerializeField] private Vector2 wind = new Vector2(1.5f, 0f);  // inclinación (x = derecha, y = frente)
    [SerializeField] private Color dropColor = new Color(0.75f, 0.82f, 0.92f, 0.4f);

    [Header("Salpicaduras")]
    [SerializeField] private bool splashEnabled = true;
    [SerializeField] private Color splashColor = new Color(0.85f, 0.9f, 1f, 0.5f);
    [SerializeField] private float splashSize = 0.09f;

    [Header("Ambiente (opcional)")]
    [SerializeField] private Light sun;                              // tu Directional Light
    [SerializeField, Range(0.2f, 1f)] private float sunMultiplier = 0.65f; // 1 = sin cambio, menos = más oscuro
    [SerializeField] private bool useFog = true;
    [SerializeField] private Color fogColor = new Color(0.45f, 0.5f, 0.55f);
    [SerializeField, Min(0f)] private float fogDensity = 0.008f;
    [SerializeField] private AudioClip rainSound;                    // un sonido de lluvia en loop (opcional)
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.5f;

    private ParticleSystem rain;
    private AudioSource audioSource;
    private float rampTime;
    private float currentIntensity = -1f;

    private float originalSunIntensity;
    private bool originalFog;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalFogDensity;
    private bool ambientApplied;

    private static Material dropMaterial;
    private static Texture2D softTexture;

    private void Start()
    {
        if (followTarget == null && Camera.main != null) followTarget = Camera.main.transform;

        CreateRain();
        ApplyAmbient();
    }

    private void OnDestroy()
    {
        RestoreAmbient();
    }

    private void Update()
    {
        if (rain == null) return;

        rampTime += Time.deltaTime;
        float ramp = fadeInTime > 0f ? Mathf.Clamp01(rampTime / fadeInTime) : 1f;
        float total = intensity * ramp;

        // Solo se actualiza cuando cambia, para no gastar de más.
        if (!Mathf.Approximately(total, currentIntensity))
        {
            currentIntensity = total;
            var emission = rain.emission;
            emission.rateOverTime = maxDropsPerSecond * total;

            if (audioSource != null) audioSource.volume = soundVolume * Mathf.Clamp01(total / Mathf.Max(intensity, 0.01f));
        }
    }

    private void LateUpdate()
    {
        if (followTarget == null || rain == null) return;
        rain.transform.position = followTarget.position + Vector3.up * height;
    }

    // ---------- Lluvia ----------

    private void CreateRain()
    {
        EnsureAssets();

        GameObject go = new GameObject("RainDrops");
        go.transform.SetParent(transform, false);

        // Las salpicaduras se crean primero para poder conectarlas.
        ParticleSystem splash = splashEnabled ? CreateSplash(go.transform) : null;

        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = rain.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = height / Mathf.Max(dropSpeed, 1f) + 0.3f;
        main.startSpeed = dropSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(dropWidth * 0.7f, dropWidth * 1.3f);
        main.startColor = dropColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4000;

        var emission = rain.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // se sube sola en Update

        var shape = rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(areaSize, areaSize, 0.1f);
        shape.rotation = new Vector3(90f, 0f, 0f); // el cuadro queda horizontal y las gotas salen hacia abajo

        var velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(wind.x, wind.x);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(wind.y, wind.y);

        // Las gotas desaparecen al tocar cualquier cosa sólida (piso, casas, personajes).
        var collision = rain.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.maxCollisionShapes = 256;
        collision.dampen = 1f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;

        if (splash != null)
        {
            var subEmitters = rain.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing);
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1f;
        renderer.velocityScale = dropLength / Mathf.Max(dropSpeed, 1f);
        renderer.cameraVelocityScale = 0f;
        renderer.sharedMaterial = dropMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        rain.Play();

        // Sonido de lluvia en loop, sin efecto 3D.
        if (rainSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = rainSound;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0f;
            audioSource.Play();
        }
    }

    // ---------- Salpicaduras: gotitas que saltan donde cae la lluvia ----------

    private ParticleSystem CreateSplash(Transform parent)
    {
        GameObject go = new GameObject("RainSplash");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(splashSize * 0.6f, splashSize * 1.3f);
        main.startColor = splashColor;
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) }); // 3 gotitas por cada gota que cae

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.02f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // salen hacia arriba

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = dropMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }

    // ---------- Ambiente ----------

    private void ApplyAmbient()
    {
        if (ambientApplied) return;
        ambientApplied = true;

        if (sun != null)
        {
            originalSunIntensity = sun.intensity;
            sun.intensity = originalSunIntensity * sunMultiplier;
        }

        originalFog = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalFogDensity = RenderSettings.fogDensity;

        if (useFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }
    }

    private void RestoreAmbient()
    {
        if (!ambientApplied) return;
        ambientApplied = false;

        if (sun != null) sun.intensity = originalSunIntensity;

        RenderSettings.fog = originalFog;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogDensity = originalFogDensity;
    }

    // ---------- Material (una sola vez) ----------

    private static void EnsureAssets()
    {
        if (softTexture == null)
        {
            const int size = 16;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softTexture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - d * d));
                }
            }
            softTexture.Apply();
        }

        if (dropMaterial == null)
        {
            dropMaterial = new Material(Shader.Find("Sprites/Default"));
            dropMaterial.mainTexture = softTexture;
        }
    }
}
