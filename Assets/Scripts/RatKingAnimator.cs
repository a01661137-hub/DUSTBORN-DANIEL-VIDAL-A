using UnityEngine;

/// <summary>
/// Animaciones del Rat King: caminar, quedarse quieto y el Zarpazo.
/// Ponlo en el objeto raíz "Rat King" (el que tiene BossAI).
/// Mide la velocidad real del boss para caminar y lee BossAI para saber cuándo ataca.
/// </summary>
public class RatKingAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;   // si lo dejas vacío lo busca en los hijos
    [SerializeField] private BossAI boss;         // si lo dejas vacío usa el del mismo objeto

    [Header("Nombres de los estados en el Animator Controller")]
    [SerializeField] private string walkStateName = "Walk";
    [SerializeField] private string idleStateName = "Idle";     // opcional: si no existe, la caminata se congela al parar
    [SerializeField] private string zarpazoStateName = "Attack"; // el estado del zarpazo

    [Header("Caminar")]
    [SerializeField] private float moveThreshold = 0.3f;
    [SerializeField] private float crossFade = 0.2f;
    [Tooltip("Velocidad del boss (m/s) a la que la animación de caminar se ve a velocidad normal. Si los pies patinan, cámbialo.")]
    [SerializeField] private float walkSpeedAtNormal = 3f;
    [SerializeField] private Vector2 animSpeedLimits = new Vector2(0.6f, 1.8f);
    [SerializeField] private float speedSmoothing = 10f;

    [Header("Zarpazo")]
    [SerializeField] private float attackCrossFade = 0.1f;
    [Tooltip("Ajusta la velocidad de la animación para que dure lo mismo que el ataque (windup + active + recovery).")]
    [SerializeField] private bool fitAnimationToAttackTime = true;
    [SerializeField] private float attackSpeedMultiplier = 1f; // si la animación va muy rápida o lenta, cámbialo
    [SerializeField] private Vector2 attackSpeedLimits = new Vector2(0.5f, 3f);

    [SerializeField] private bool debugLogs = false;

    private Vector3 lastPosition;
    private float smoothedSpeed;
    private bool walking;
    private bool hasIdle;
    private bool started;
    private bool attacking;
    private int lastAttackSerial;
    private float attackClipSpeed = 1f;
    private float settleTimer;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (boss == null) boss = GetComponent<BossAI>();
        lastPosition = transform.position;
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogWarning("[RatKingAnimator] No hay Animator en el Rat King ni en sus hijos.", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        hasIdle = animator.HasState(0, Animator.StringToHash(idleStateName));
        if (boss != null) lastAttackSerial = boss.AttackSerial;

        if (!animator.HasState(0, Animator.StringToHash(walkStateName)))
            Debug.LogWarning("[RatKingAnimator] El Animator no tiene un estado llamado '" + walkStateName + "'.", this);
        if (!animator.HasState(0, Animator.StringToHash(zarpazoStateName)))
            Debug.LogWarning("[RatKingAnimator] El Animator no tiene un estado llamado '" + zarpazoStateName + "' (el zarpazo).", this);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || animator == null) return;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        float speed = delta.magnitude / dt;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, speedSmoothing * dt);

        // ---- Zarpazo ----
        if (boss != null && boss.AttackSerial != lastAttackSerial)
        {
            lastAttackSerial = boss.AttackSerial;
            if (boss.CurrentAttackShape == AttackShape.TipSphere)
                StartZarpazo();
        }

        if (attacking)
        {
            if (boss != null && boss.IsAttacking)
            {
                animator.speed = attackClipSpeed;
                return; // mientras ataca no se toca nada más
            }

            // Terminó el ataque: vuelve a caminar o a quedarse quieto.
            attacking = false;
            started = false;
        }

        // ---- Caminar / quieto ----
        bool shouldWalk = smoothedSpeed > moveThreshold;

        if (!started || shouldWalk != walking)
        {
            started = true;
            walking = shouldWalk;
            settleTimer = crossFade + 0.05f;

            if (walking || !hasIdle)
                animator.CrossFadeInFixedTime(walkStateName, crossFade, 0);
            else
                animator.CrossFadeInFixedTime(idleStateName, crossFade, 0);

            if (debugLogs) Debug.Log("[RatKingAnimator] " + (walking ? "Camina" : "Quieto") + " (" + smoothedSpeed.ToString("F2") + " m/s)");
        }

        if (walking)
        {
            float s = smoothedSpeed / Mathf.Max(0.1f, walkSpeedAtNormal);
            animator.speed = Mathf.Clamp(s, animSpeedLimits.x, animSpeedLimits.y);
        }
        else
        {
            settleTimer -= dt;
            // Sin animación de reposo: se deja avanzar un momento para completar la transición y luego se congela.
            animator.speed = (hasIdle || settleTimer > 0f) ? 1f : 0f;
        }
    }

    private void StartZarpazo()
    {
        attacking = true;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(zarpazoStateName, attackCrossFade, 0, 0f);

        attackClipSpeed = 1f;
        if (fitAnimationToAttackTime && boss != null)
        {
            animator.Update(0f); // para poder leer la duración del estado nuevo
            float length = animator.GetNextAnimatorStateInfo(0).length;
            if (length <= 0.01f) length = animator.GetCurrentAnimatorStateInfo(0).length;
            if (length > 0.01f)
                attackClipSpeed = Mathf.Clamp(length / boss.CurrentAttackDuration, attackSpeedLimits.x, attackSpeedLimits.y);
        }
        attackClipSpeed *= attackSpeedMultiplier;

        if (debugLogs) Debug.Log("[RatKingAnimator] Zarpazo, velocidad de animación " + attackClipSpeed.ToString("F2"));
    }
}