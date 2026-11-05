using UnityEngine;

/// <summary>
/// Crea nubes de polvo y trocitos de tierra por código, sin necesidad de prefabs ni texturas.
/// Se usa desde BossAI cuando un ataque golpea el suelo.
/// </summary>
public static class ImpactDust
{
    private static Texture2D softTexture;
    private static Material particleMaterial;

    /// <param name="position">Dónde sale el efecto (en el piso).</param>
    /// <param name="spread">Radio del área donde nace el polvo.</param>
    /// <param name="puffSize">Tamaño de cada nube de polvo.</param>
    /// <param name="count">Cantidad de nubes.</param>
    /// <param name="withDebris">Si también salen trocitos de tierra que saltan.</param>
    public static void Burst(Vector3 position, float spread, float puffSize, int count,
        Color dustColor, Color debrisColor, bool withDebris = true)
    {
        EnsureAssets();

        GameObject root = new GameObject("ImpactDust");
        root.transform.position = position;

        CreateDust(root.transform, spread, puffSize, count, dustColor);
        if (withDebris) CreateDebris(root.transform, spread, puffSize, debrisColor);

        Object.Destroy(root, 4f);
    }

    // ---------- Polvo: nubes suaves que se expanden por el piso y se desvanecen ----------

    private static void CreateDust(Transform parent, float spread, float puffSize, int count, Color color)
    {
        GameObject go = new GameObject("Dust");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spread * 0.4f, spread * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(puffSize * 0.6f, puffSize * 1.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = -0.03f; // sube muy poquito
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(spread * 0.5f, 0.05f);
        shape.radiusThickness = 0.3f;
        shape.rotation = new Vector3(90f, 0f, 0f); // círculo acostado sobre el piso

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(puffSize * 0.15f, puffSize * 0.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.4f));

        SetupRenderer(go);
        ps.Play();
    }

    // ---------- Tierra: trocitos oscuros que salen despedidos hacia arriba y caen ----------

    private static void CreateDebris(Transform parent, float spread, float puffSize, Color color)
    {
        GameObject go = new GameObject("Debris");
        go.transform.SetParent(parent, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spread * 0.8f + 2f, spread * 1.6f + 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(puffSize * 0.08f, puffSize * 0.2f);
        main.startColor = color;
        main.gravityModifier = 2.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = Mathf.Max(spread * 0.4f, 0.05f);
        shape.rotation = new Vector3(-90f, 0f, 0f); // hemisferio mirando hacia arriba

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        SetupRenderer(go);
        ps.Play();
    }

    // ---------- Material y textura (se crean una sola vez) ----------

    private static void SetupRenderer(GameObject go)
    {
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void EnsureAssets()
    {
        if (softTexture == null)
        {
            const int size = 64;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softTexture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float alpha = Mathf.Pow(1f - d, 2f); // centro opaco, bordes suaves
                    softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            softTexture.Apply();
        }

        if (particleMaterial == null)
        {
            particleMaterial = new Material(Shader.Find("Sprites/Default"));
            particleMaterial.mainTexture = softTexture;
        }
    }
}