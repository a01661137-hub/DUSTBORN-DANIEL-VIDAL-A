using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de muerte estilo soulslike: cámara lenta, texto "HAS MUERTO" que aparece poco a poco,
/// fundido a negro y reinicio de la escena (el boss, los frascos y todo vuelven a empezar).
/// Ponlo en un objeto vacío (por ejemplo "GameManager") y arrastra a ROACH en Player Health.
/// </summary>
public class DeathScreen : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Health playerHealth; // el Health de ROACH

    [Header("Texto")]
    [SerializeField] private string deathText = "HAS MUERTO";
    [SerializeField] private Color textColor = new Color(0.7f, 0.1f, 0.1f);
    [SerializeField] private int fontSize = 64;

    [Header("Tiempos (segundos reales)")]
    [SerializeField] private float textFadeIn = 1.5f;   // cuánto tarda en aparecer el texto
    [SerializeField] private float holdTime = 2.5f;     // cuánto se queda el texto
    [SerializeField] private float fadeToBlack = 1f;    // fundido final antes de reiniciar

    [Header("Cámara lenta")]
    [SerializeField] private bool slowMotion = true;
    [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.3f;
    [SerializeField] private float slowMotionTime = 1.2f;

    [Header("Fondo oscuro mientras sale el texto")]
    [SerializeField, Range(0f, 1f)] private float dimAmount = 0.5f;

    private float textAlpha;
    private float dimAlpha;
    private float blackAlpha;
    private bool dead;
    private GUIStyle style;

    private void Start()
    {
#pragma warning disable CS0618 // en versiones nuevas de Unity este método está marcado como viejo, pero funciona
        if (playerHealth == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) playerHealth = player.GetComponent<Health>();
        }
#pragma warning restore CS0618

        if (playerHealth == null)
        {
            Debug.LogError("DeathScreen: arrastra a ROACH al campo Player Health.", this);
            enabled = false;
            return;
        }

        Time.timeScale = 1f; // por si alguna vez se quedó en cámara lenta
        playerHealth.Died += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        Time.timeScale = 1f;
    }

    private void OnPlayerDied()
    {
        if (dead) return;
        dead = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (slowMotion) Time.timeScale = slowMotionScale;

        // 1) El texto aparece y la pantalla se oscurece un poco.
        float t = 0f;
        while (t < textFadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(textFadeIn, 0.01f));
            textAlpha = k;
            dimAlpha = dimAmount * k;

            if (slowMotion && t >= slowMotionTime) Time.timeScale = 1f;
            yield return null;
        }

        Time.timeScale = 1f;

        // 2) El texto se queda un momento.
        yield return new WaitForSecondsRealtime(holdTime);

        // 3) Fundido a negro.
        t = 0f;
        while (t < fadeToBlack)
        {
            t += Time.unscaledDeltaTime;
            blackAlpha = Mathf.Clamp01(t / Mathf.Max(fadeToBlack, 0.01f));
            yield return null;
        }

        blackAlpha = 1f;
        ReloadScene();
    }

    private void ReloadScene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }

#if UNITY_EDITOR
        // La escena no está en Build Settings: en el editor se puede recargar igual.
        Debug.LogWarning("DeathScreen: agrega la escena en File > Build Settings (Add Open Scenes) para que funcione también al compilar el juego.", this);
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError("DeathScreen: la escena no está en Build Settings, no se puede reiniciar.", this);
#endif
    }

    // Se dibuja encima de todo lo demás (depth negativo).
    private void OnGUI()
    {
        if (!dead || Event.current.type != EventType.Repaint) return;

        GUI.depth = -100;

        if (dimAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        if (textAlpha > 0.001f)
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            Rect rect = new Rect(0f, 0f, Screen.width, Screen.height);

            style.normal.textColor = new Color(0f, 0f, 0f, textAlpha * 0.8f);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), deathText, style);

            style.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, textAlpha);
            GUI.Label(rect, deathText, style);
        }

        if (blackAlpha > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, blackAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }
}
