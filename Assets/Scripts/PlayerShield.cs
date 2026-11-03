using UnityEngine;

public enum HitOutcome { Hit, Blocked, Parried, GuardBroken }

/// <summary>
/// Escudo con dos usos:
///  - Mantener la tecla: BLOQUEO. Reduce el daño de golpes frontales a cambio de stamina.
///  - Pulsar justo antes de que llegue el golpe: PARRY. Anula el daño y aturde al enemigo.
/// Si te quedas sin stamina al bloquear, se rompe la guardia y recibes el golpe completo.
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(Health))]
public class PlayerShield : MonoBehaviour
{
    [Header("Controles")]
    [SerializeField] private KeyCode guardKey = KeyCode.F;

    [Header("Bloqueo")]
    [SerializeField] private float guardAngle = 140f; // arco frontal que protege el escudo
    [SerializeField, Range(0f, 1f)] private float blockDamageReduction = 0.8f; // 0.8 = recibes 20%
    [SerializeField] private float staminaPerDamage = 1f; // stamina gastada por cada punto de daño bloqueado
    [SerializeField, Range(0.1f, 1f)] private float guardMoveMultiplier = 0.6f;
    [SerializeField] private float guardBreakLockTime = 1.2f; // tiempo sin poder usar el escudo tras romperse

    [Header("Parry")]
    [SerializeField] private float parryWindow = 0.25f;   // tiempo tras pulsar en el que el golpe se parrea
    [SerializeField] private float parryCooldown = 0.8f;  // evita parrear aporreando la tecla

    [Header("Visual (opcional)")]
    [SerializeField] private GameObject shieldVisual;
    [SerializeField] private bool visibleWhenIdle = true; // si es falso, solo se ve mientras guardas
    [SerializeField] private Color blockColor = new Color(0.55f, 0.6f, 0.7f);
    [SerializeField] private Color parryColor = Color.cyan;
    [SerializeField] private Color blockHitColor = Color.white;

    private PlayerController player;
    private Health health;
    private Material shieldMaterial;

    private float parryEndTime;
    private float nextParryTime;
    private float guardLockedUntil;
    private float flashUntil;
    private Color flashColor;

    public bool IsGuarding { get; private set; }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        health = GetComponent<Health>();

        if (shieldVisual != null)
        {
            Renderer r = shieldVisual.GetComponentInChildren<Renderer>(true);
            if (r != null)
            {
                shieldMaterial = r.material; // instancia propia
                shieldMaterial.color = blockColor;
            }
            shieldVisual.SetActive(visibleWhenIdle);
        }
    }

    private void Update()
    {
        bool canGuard = !health.IsDead
                        && !player.IsRolling
                        && !player.IsDrinking
                        && !player.ActionLocked
                        && Time.time >= guardLockedUntil;

        bool wantsGuard = canGuard && Input.GetKey(guardKey);

        if (wantsGuard && !IsGuarding) StartGuard(Input.GetKeyDown(guardKey));
        else if (!wantsGuard && IsGuarding) StopGuard();

        if (IsGuarding) UpdateVisual();
    }

    private void StartGuard(bool freshPress)
    {
        IsGuarding = true;
        player.IsGuarding = true;
        player.SpeedMultiplier = guardMoveMultiplier;

        // La ventana de parry solo se abre al pulsar (no al mantener) y respeta el cooldown.
        if (freshPress && Time.time >= nextParryTime)
        {
            parryEndTime = Time.time + parryWindow;
            nextParryTime = Time.time + parryCooldown;
        }

        if (shieldVisual != null) shieldVisual.SetActive(true);
    }

    private void StopGuard()
    {
        IsGuarding = false;
        player.IsGuarding = false;
        if (!player.IsDrinking) player.SpeedMultiplier = 1f;

        if (shieldVisual != null) shieldVisual.SetActive(visibleWhenIdle);
        if (shieldMaterial != null) shieldMaterial.color = blockColor;
    }

    /// <summary>
    /// Lo llaman los enemigos antes de hacer daño. Decide qué pasa con el golpe.
    /// </summary>
    /// <param name="finalDamage">Daño que debe aplicarse al jugador tras bloquear/parrear.</param>
    public HitOutcome ResolveHit(float damage, Transform attacker, out float finalDamage)
    {
        finalDamage = damage;
        if (!IsGuarding) return HitOutcome.Hit;

        // El escudo solo protege de golpes que vienen de frente.
        Vector3 toAttacker = attacker.position - transform.position;
        toAttacker.y = 0f;
        if (Vector3.Angle(transform.forward, toAttacker) > guardAngle * 0.5f) return HitOutcome.Hit;

        // Parry: el golpe llega dentro de la ventana de tiempo.
        if (Time.time < parryEndTime)
        {
            finalDamage = 0f;
            parryEndTime = 0f;
            Flash(parryColor, 0.35f);
            return HitOutcome.Parried;
        }

        // Bloqueo normal: cuesta stamina.
        float staminaCost = damage * staminaPerDamage;
        if (player.TryConsumeStamina(staminaCost))
        {
            finalDamage = damage * (1f - blockDamageReduction);
            Flash(blockHitColor, 0.12f);
            return HitOutcome.Blocked;
        }

        // Sin stamina: la guardia se rompe y el golpe entra completo.
        player.TryConsumeStamina(player.Stamina);
        guardLockedUntil = Time.time + guardBreakLockTime;
        StopGuard();
        return HitOutcome.GuardBroken;
    }

    private void Flash(Color color, float duration)
    {
        flashColor = color;
        flashUntil = Time.time + duration;
    }

    // Cian = ventana de parry, gris = bloqueo normal, blanco/cian = reacción al golpe.
    private void UpdateVisual()
    {
        if (shieldMaterial == null) return;

        if (Time.time < flashUntil) shieldMaterial.color = flashColor;
        else if (Time.time < parryEndTime) shieldMaterial.color = parryColor;
        else shieldMaterial.color = blockColor;
    }
}
