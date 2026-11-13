using UnityEngine;

/// <summary>
/// Base de movimiento estilo soulslike: caminar/correr, esquiva (roll) con i-frames
/// y sistema de stamina. Requiere un CharacterController en el objeto.
/// Usa el Input Manager clásico (Horizontal, Vertical, Fire3 = correr, Jump = esquivar).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -20f;

    [Header("Suavizado de movimiento")]
    [SerializeField] private float acceleration = 25f; // qué tan rápido alcanza la velocidad (más alto = más brusco)
    [SerializeField] private float deceleration = 30f; // qué tan rápido frena al soltar las teclas

    [Header("Cámara fijada (lock-on)")]
    [Tooltip("Si está desmarcado, no puedes correr mientras la cámara está fijada a un enemigo.")]
    [SerializeField] private bool runWhileLockedOn = false;
    [Tooltip("Velocidad base del roll con la cámara fijada (menor que Roll Walk Speed = recorre menos distancia).")]
    [SerializeField] private float rollLockedSpeed = 7f;
    [Tooltip("Duración del roll con la cámara fijada. Menor que Roll Walk Duration = la animación se ve más rápida.")]
    [SerializeField] private float rollLockedDuration = 0.5f;
    [Tooltip("Tiempo de levantarse después del roll con la cámara fijada.")]
    [SerializeField] private float rollLockedRecoveryTime = 0.15f;

    [Header("Esquivar (Roll)")]
    [SerializeField] private float rollWalkSpeed = 10f;   // velocidad base del roll si estabas caminando o quieto
    [SerializeField] private float rollRunSpeed = 15f;    // velocidad base del roll si venías corriendo
    [SerializeField] private float rollRunThreshold = 4.5f; // desde qué velocidad cuenta como "venía corriendo"
    [Tooltip("Multiplica la velocidad durante el roll: izquierda = inicio, derecha = final. Arranca rápido y termina lento.")]
    [SerializeField]
    private AnimationCurve rollSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1.4f), new Keyframe(1f, 0.5f));
    [SerializeField] private float rollWalkDuration = 0.7f; // cuánto dura el roll caminando (más largo = animación más lenta)
    [SerializeField] private float rollRunDuration = 0.5f;  // cuánto dura el roll corriendo
    [Tooltip("Tiempo después del roll CAMINANDO en que te levantas: puedes avanzar pero más lento, sin correr ni esquivar.")]
    [SerializeField] private float rollRecoveryTime = 0.3f;
    [Tooltip("Lo mismo pero para el roll CORRIENDO. 0 = sin recuperación (el roll corriendo queda como antes).")]
    [SerializeField] private float rollRunRecoveryTime = 0f;
    [Header("Roll corriendo: variante de barrido")]
    [Tooltip("Probabilidad (0 a 1) de que el roll corriendo sea el barrido en vez del roll normal. 0.5 = mitad y mitad, 0 = nunca.")]
    [SerializeField, Range(0f, 1f)] private float rollRunAltChance = 0.5f;
    [SerializeField] private float rollRunAltSpeed = 12f;       // velocidad base del barrido
    [SerializeField] private float rollRunAltDuration = 0.6f;   // duración del barrido (más larga = animación más lenta)
    [SerializeField] private float rollRunAltRecoveryTime = 0f; // recuperación después del barrido

    [Header("Roll: impulso")]
    [Tooltip("Al terminar el roll corriendo, conserva el impulso (frena poco a poco, o sigue corriendo si mantienes W) en vez de detenerse en seco.")]
    [SerializeField] private bool rollRunKeepsMomentum = true;
    [Tooltip("Velocidad al empezar a levantarte (0.35 = 35%). Sube poco a poco hasta el 100%.")]
    [SerializeField, Range(0f, 1f)] private float rollRecoverySpeedMultiplier = 0.35f;
    [Tooltip("Aceleración justo después de un roll. Más bajo = el cambio de velocidad es más lento y suave.")]
    [SerializeField] private float rollExitAcceleration = 6f;
    [Tooltip("Cuántos segundos dura esa aceleración suave después del roll.")]
    [SerializeField] private float rollExitAccelTime = 0.6f;
    [Tooltip("Qué tan rápido gira la dirección del impulso hacia donde apuntas después del roll (grados por segundo). Más alto = patina menos.")]
    [SerializeField] private float rollExitTurnSpeed = 720f;
    [SerializeField] private float invulnerableTime = 0.25f; // i-frames al inicio del roll
    [SerializeField] private float rollStaminaCost = 20f;

    [Header("Derribo (Embestida y Barrido del jefe)")]
    [Tooltip("Fase 1 del derribo: cuánto dura la animación de caer y quedarte en el suelo (hasta que empieza a levantarse). La animación se ajusta a este tiempo.")]
    [SerializeField] private float knockdownDuration = 1.0f;
    [Tooltip("Fase 2 del derribo: cuánto dura la animación de levantarse. La animación se ajusta a este tiempo.")]
    [SerializeField] private float getUpDuration = 1.0f;
    [Tooltip("Tiempo extra bloqueado DESPUÉS de que termina la animación de levantarse, mientras el Animator regresa a la normalidad. Iguala o supera la Transition Duration de la flecha GetUp > Locomotion (0.25).")]
    [SerializeField] private float getUpExitLock = 0.3f;
    [Tooltip("Si está marcado, eres invulnerable también mientras te levantas. Desmárcalo para que el jefe pueda golpearte en ese momento.")]
    [SerializeField] private bool getUpInvulnerable = true;
    [Tooltip("Cuántos metros te lanza hacia atrás al derribarte (si no hay una pared en medio).")]
    [SerializeField] private float knockbackDistance = 3.5f;
    [Tooltip("En cuánto tiempo recorres esa distancia. Debe cubrir la parte de la animación en que sales volando, antes de caer al suelo.")]
    [SerializeField] private float knockbackTime = 0.7f;
    [Tooltip("Gira al personaje hacia el jefe al ser derribado (para animaciones de caer de espaldas). Desmárcalo si tu animación cae hacia adelante.")]
    [SerializeField] private bool knockdownFaceAttacker = true;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenPerSecond = 25f;
    [SerializeField] private float staminaRegenDelay = 1f;
    [SerializeField] private float runStaminaPerSecond = 10f;

    [Header("Referencias")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;

    private CharacterController controller;
    private Health health;
    private Vector3 verticalVelocity;
    private Vector3 currentVelocity; // velocidad horizontal suavizada
    private Vector3 rollDirection;
    private float currentRollSpeed;
    private float currentRollDuration = 0.5f;
    private float recoveryTimer;
    private float exitAccelTimer;
    private float currentRecoveryTime;
    private bool rollWasRun;
    private bool rollIsAlt;
    private float rollTimer;
    private float regenTimer;
    private float knockdownTimer;
    private Vector3 knockbackDirection;
    private float knockbackElapsed;
    private bool actionLocked;

    public float Stamina { get; private set; }
    public float MaxStamina => maxStamina;
    public bool IsRolling => rollTimer > 0f;
    public bool IsInvulnerable => rollTimer > currentRollDuration - invulnerableTime;

    // Duración del roll en curso (la lee PlayerAnimator para ajustar la animación).
    public float CurrentRollDuration => currentRollDuration;

    // Qué roll se eligió (lo lee PlayerAnimator para reproducir la animación correcta).
    public bool CurrentRollIsRun => rollWasRun;
    public bool CurrentRollIsAlt => rollIsAlt;

    // Fase en que te levantas después de rodar (la lee PlayerAnimator).
    public bool IsRollRecovering => recoveryTimer > 0f;
    public float RollRecoveryTime => currentRecoveryTime;

    // Derribado por el jefe: no puedes moverte, atacar, guardar ni curarte hasta levantarte.
    public bool IsKnockedDown => knockdownTimer > 0f;
    public bool IsGettingUp => knockdownTimer > 0f && knockdownTimer <= getUpDuration + getUpExitLock; // fase 2: levantándote
    public float KnockdownDuration => knockdownDuration;

    // La cámara está fijada a un enemigo (lo lee PlayerAnimator para el caminar de lado).
    public bool IsLockedOn => thirdPersonCamera != null && thirdPersonCamera.IsLockedOn;
    public float GetUpDuration => getUpDuration;

    // Lo activa PlayerCombat mientras ataca: bloquea movimiento y esquiva.
    // También cuenta como bloqueado mientras estás derribado.
    public bool ActionLocked
    {
        get => actionLocked || IsKnockedDown;
        set => actionLocked = value;
    }

    /// <summary>Lo llama BossAI al derribarte. fromPosition = de dónde viene el golpe.</summary>
    public void StartKnockdown(Vector3 fromPosition)
    {
        if (IsKnockedDown || (health != null && health.IsDead)) return;

        // Cancela cualquier roll o recuperación en curso.
        rollTimer = 0f;
        recoveryTimer = 0f;
        currentVelocity = Vector3.zero;

        Vector3 away = transform.position - fromPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        knockbackDirection = away;
        knockbackElapsed = 0f;
        if (knockdownFaceAttacker) transform.rotation = Quaternion.LookRotation(-away);

        knockdownTimer = knockdownDuration + getUpDuration + getUpExitLock; // caer + levantarse + regresar a la normalidad
        regenTimer = staminaRegenDelay;
    }

    // Lo usa PlayerHealing mientras bebe: más lento, sin correr, sin esquivar ni atacar.
    public bool IsDrinking { get; set; }

    // Lo usa PlayerShield mientras el escudo está arriba.
    public bool IsGuarding { get; set; }
    public float SpeedMultiplier { get; set; } = 1f;

    // Gasta stamina si alcanza. Reinicia el retraso de regeneración.
    public bool TryConsumeStamina(float amount)
    {
        if (Stamina < amount) return false;

        Stamina -= amount;
        regenTimer = staminaRegenDelay;
        return true;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        Stamina = maxStamina;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        Vector3 input = GetCameraRelativeInput();

        if (knockdownTimer > 0f) knockdownTimer -= Time.deltaTime;

        // Invulnerable en los i-frames del roll y mientras estás derribado (y levantándote, si así lo eliges).
        bool downInvulnerable = IsKnockedDown && (!IsGettingUp || getUpInvulnerable);
        if (health != null) health.Invulnerable = IsInvulnerable || downInvulnerable;
        bool dead = health != null && health.IsDead;

        if (!IsRolling && recoveryTimer > 0f) recoveryTimer -= Time.deltaTime;
        if (exitAccelTimer > 0f) exitAccelTimer -= Time.deltaTime;

        if (IsKnockedDown)
        {
            HandleKnockdown();
        }
        else if (IsRolling)
        {
            currentVelocity = Vector3.zero;
            HandleRoll();
        }
        else if (!ActionLocked && !dead)
        {
            TryStartRoll(input);
            HandleMovement(input);
        }
        else
        {
            // Atacando o muerto: se frena con suavidad en vez de cortarse en seco.
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            if (currentVelocity.sqrMagnitude > 0.0001f) controller.Move(currentVelocity * Time.deltaTime);
        }

        ApplyGravity();
        HandleStaminaRegen();
    }

    // Derribado: sales lanzado hacia atrás. Arrancas rápido y frenas poco a poco hasta
    // recorrer exactamente knockbackDistance metros en knockbackTime segundos.
    private void HandleKnockdown()
    {
        currentVelocity = Vector3.zero;

        float time = Mathf.Max(knockbackTime, 0.05f);
        if (knockbackElapsed >= time) return;

        float t = knockbackElapsed / time;                 // 0 al inicio, 1 al final
        float startSpeed = 2f * knockbackDistance / time;  // la velocidad baja linealmente hasta 0
        float speed = startSpeed * (1f - t);

        controller.Move(knockbackDirection * speed * Time.deltaTime);
        knockbackElapsed += Time.deltaTime;
    }

    // Dirección de input relativa a la cámara, sobre el plano horizontal.
    private Vector3 GetCameraRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;

        return (forward.normalized * v + right.normalized * h).normalized;
    }

    private void HandleMovement(Vector3 input)
    {
        bool locked = thirdPersonCamera != null && thirdPersonCamera.IsLockedOn;
        if (locked) FaceLockedTarget();

        bool hasInput = input.sqrMagnitude >= 0.01f;
        Vector3 targetVelocity = Vector3.zero;

        if (hasInput)
        {
            bool recovering = recoveryTimer > 0f;
            bool wantsRun = Input.GetButton("Fire3") && Stamina > 0f && !IsDrinking && !IsGuarding && !recovering
                            && (!locked || runWhileLockedOn);
            float speed = (wantsRun ? runSpeed : walkSpeed) * SpeedMultiplier;

            // Al levantarte del roll avanzas más lento y la velocidad vuelve poco a poco a la normal.
            if (recovering)
            {
                float progress = 1f - Mathf.Clamp01(recoveryTimer / Mathf.Max(currentRecoveryTime, 0.01f));
                speed *= Mathf.Lerp(rollRecoverySpeedMultiplier, 1f, progress);
            }
            targetVelocity = input * speed;

            if (wantsRun)
            {
                Stamina = Mathf.Max(0f, Stamina - runStaminaPerSecond * Time.deltaTime);
                regenTimer = staminaRegenDelay;
            }

            // Sin lock-on el personaje mira hacia donde camina; con lock-on sigue mirando al enemigo (strafe).
            if (!locked)
            {
                Quaternion targetRot = Quaternion.LookRotation(input);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        // Acelera al empezar y frena al soltar, en lugar de saltar de 0 a la velocidad máxima.
        float rate = hasInput ? acceleration : deceleration;
        if (hasInput && exitAccelTimer > 0f) rate = Mathf.Min(rate, rollExitAcceleration);
        if (hasInput && exitAccelTimer > 0f && currentVelocity.sqrMagnitude > 0.01f && targetVelocity.sqrMagnitude > 0.01f)
        {
            // Justo después del roll la velocidad cambia de módulo suavemente, pero la DIRECCIÓN gira
            // rápido hacia donde apuntas, para que no patine hacia donde ibas antes de mover la cámara.
            float mag = Mathf.MoveTowards(currentVelocity.magnitude, targetVelocity.magnitude, rate * Time.deltaTime);
            Vector3 dir = Vector3.RotateTowards(currentVelocity.normalized, targetVelocity.normalized,
                rollExitTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
            currentVelocity = dir * mag;
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);
        }

        if (currentVelocity.sqrMagnitude > 0.0001f)
            controller.Move(currentVelocity * Time.deltaTime);
    }

    private void FaceLockedTarget()
    {
        Vector3 toTarget = thirdPersonCamera.LockedAimPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(toTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    private void TryStartRoll(Vector3 input)
    {
        if (!Input.GetButtonDown("Jump")) return;
        if (IsDrinking) return;
        if (recoveryTimer > 0f) return; // no se puede encadenar otro roll mientras te levantas
        if (Stamina < rollStaminaCost) return;

        // Si venías corriendo, el roll recorre más distancia que si venías caminando.
        bool lockedOn = thirdPersonCamera != null && thirdPersonCamera.IsLockedOn;
        bool wasRunning = !lockedOn && currentVelocity.magnitude >= rollRunThreshold;
        currentRollSpeed = wasRunning ? rollRunSpeed : rollWalkSpeed;
        currentRollDuration = wasRunning ? rollRunDuration : rollWalkDuration;
        currentRecoveryTime = wasRunning ? rollRunRecoveryTime : rollRecoveryTime;
        rollWasRun = wasRunning;

        // Con la cámara fijada el roll es siempre el de caminar, pero más corto y rápido.
        if (lockedOn)
        {
            currentRollSpeed = rollLockedSpeed;
            currentRollDuration = rollLockedDuration;
            currentRecoveryTime = rollLockedRecoveryTime;
        }

        // Corriendo: al azar entre el roll normal y el barrido.
        rollIsAlt = wasRunning && Random.value < rollRunAltChance;
        if (rollIsAlt)
        {
            currentRollSpeed = rollRunAltSpeed;
            currentRollDuration = rollRunAltDuration;
            currentRecoveryTime = rollRunAltRecoveryTime;
        }

        Stamina -= rollStaminaCost;
        regenTimer = staminaRegenDelay;
        rollTimer = currentRollDuration;
        // Si no hay input, esquiva hacia donde mira el personaje.
        rollDirection = input.sqrMagnitude > 0.01f ? input : transform.forward;
        transform.rotation = Quaternion.LookRotation(rollDirection);
    }

    private void HandleRoll()
    {
        rollTimer -= Time.deltaTime;

        float progress = 1f - Mathf.Clamp01(rollTimer / Mathf.Max(currentRollDuration, 0.01f));
        float speed = currentRollSpeed * Mathf.Max(0f, rollSpeedCurve.Evaluate(progress));
        controller.Move(rollDirection * speed * Time.deltaTime);

        if (rollTimer <= 0f)
        {
            recoveryTimer = currentRecoveryTime; // empieza a levantarse (0 = sin recuperación)
            exitAccelTimer = rollExitAccelTime;  // la velocidad cambia suave después del roll

            // Si el roll fue corriendo, sigues con parte del impulso en vez de frenar en seco.
            if (rollWasRun && rollRunKeepsMomentum)
                currentVelocity = rollDirection * Mathf.Min(speed, runSpeed);
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    private void HandleStaminaRegen()
    {
        if (IsRolling || IsGuarding) return;

        if (regenTimer > 0f)
        {
            regenTimer -= Time.deltaTime;
            return;
        }

        Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.deltaTime);
    }
}