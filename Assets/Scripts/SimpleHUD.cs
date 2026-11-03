using UnityEngine;

/// <summary>
/// HUD temporal con OnGUI: barra de vida (roja) y de stamina (verde).
/// Ponlo en cualquier objeto vacío de la escena y asigna al jugador.
/// Más adelante se reemplaza por un Canvas de verdad.
/// </summary>
public class SimpleHUD : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerHealing healing; // opcional: muestra los frascos restantes
    [SerializeField] private BossAI boss; // opcional: muestra su barra al pelear

    private GUIStyle deathStyle;
    private GUIStyle bossStyle;
    private GUIStyle victoryStyle;

    private Health bossHealth;
    private string defeatedBossName = "";
    private float bossDefeatedAt = -1f;

    private void Start()
    {
        if (boss != null)
        {
            bossHealth = boss.Health;
            bossHealth.Died += OnBossDied;
        }
    }

    private void OnDestroy()
    {
        if (bossHealth != null) bossHealth.Died -= OnBossDied;
    }

    // Guardamos el nombre ahora porque el boss se destruye medio segundo después de morir.
    private void OnBossDied()
    {
        defeatedBossName = boss != null ? boss.BossName : "Jefe";
        bossDefeatedAt = Time.time;
    }

    private void OnGUI()
    {
        if (player == null || playerHealth == null) return;

        DrawBar(new Rect(20, 20, 320, 20), playerHealth.Current / playerHealth.Max, new Color(0.8f, 0.1f, 0.1f));
        DrawBar(new Rect(20, 46, 240, 14), player.Stamina / player.MaxStamina, new Color(0.2f, 0.7f, 0.2f));

        if (healing != null)
            GUI.Label(new Rect(20, 66, 240, 24), "Frascos: " + healing.Charges + "/" + healing.MaxCharges);

        if (boss != null && boss.IsEngaged && !boss.Health.IsDead)
            DrawBossBar();

        if (bossDefeatedAt >= 0f)
            DrawVictoryText();

        if (playerHealth.IsDead)
        {
            if (deathStyle == null)
            {
                deathStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                deathStyle.normal.textColor = new Color(0.7f, 0.1f, 0.1f);
            }

            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "HAS MUERTO", deathStyle);
        }
    }

    // "NOMBRE ELIMINADO": aparece con fundido, se queda unos segundos y se va.
    private void DrawVictoryText()
    {
        const float fadeIn = 0.6f;
        const float hold = 3f;
        const float fadeOut = 1.2f;

        float elapsed = Time.time - bossDefeatedAt;
        if (elapsed > fadeIn + hold + fadeOut) return;

        float alpha;
        if (elapsed < fadeIn) alpha = elapsed / fadeIn;
        else if (elapsed < fadeIn + hold) alpha = 1f;
        else alpha = 1f - (elapsed - fadeIn - hold) / fadeOut;

        if (victoryStyle == null)
        {
            victoryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        string text = defeatedBossName.ToUpper() + " ELIMINADO";
        Rect rect = new Rect(0f, Screen.height * 0.35f, Screen.width, 80f);

        // Sombra para que se lea sobre cualquier fondo.
        victoryStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, victoryStyle);

        victoryStyle.normal.textColor = new Color(0.95f, 0.75f, 0.25f, alpha);
        GUI.Label(rect, text, victoryStyle);
    }

    private void DrawBossBar()
    {
        float width = Mathf.Min(600f, Screen.width - 80f);
        float x = (Screen.width - width) / 2f;
        float y = Screen.height - 50f;

        if (bossStyle == null)
        {
            bossStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            bossStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(x, y - 28f, width, 24f), boss.BossName, bossStyle);
        DrawBar(new Rect(x, y, width, 16f), boss.Health.Current / boss.Health.Max, new Color(0.7f, 0.15f, 0.1f));
    }

    private static void DrawBar(Rect rect, float fill, Color color)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}