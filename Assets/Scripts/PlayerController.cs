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

    [Header("Esquivar (Roll)")]
    [SerializeField] private float rollSpeed = 9f;
    [SerializeField] private float rollDuration = 0.45f;
    [SerializeField] private float invulnerableTime = 0.25f; // i-frames al inicio del roll
    [SerializeField] private float rollStaminaCost = 20f;

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
    private float rollTimer;
    private float regenTimer;

    public float Stamina { get; private set; }
    public float MaxStamina => maxStamina;
    public bool IsRolling => rollTimer > 0f;
    public bool IsInvulnerable => rollTimer > rollDuration - invulnerableTime;

    // Lo activa PlayerCombat mientras ataca: bloquea movimiento y esquiva.
    public bool ActionLocked { get; set; }

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

        if (health != null) health.Invulnerable = IsInvulnerable;
        bool dead = health != null && health.IsDead;

        if (IsRolling)
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
            bool wantsRun = Input.GetButton("Fire3") && Stamina > 0f && !IsDrinking && !IsGuarding;
            float speed = (wantsRun ? runSpeed : walkSpeed) * SpeedMultiplier;
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
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);

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
        if (Stamina < rollStaminaCost) return;

        Stamina -= rollStaminaCost;
        regenTimer = staminaRegenDelay;
        rollTimer = rollDuration;
        // Si no hay input, esquiva hacia donde mira el personaje.
        rollDirection = input.sqrMagnitude > 0.01f ? input : transform.forward;
        transform.rotation = Quaternion.LookRotation(rollDirection);
    }

    private void HandleRoll()
    {
        rollTimer -= Time.deltaTime;
        controller.Move(rollDirection * rollSpeed * Time.deltaTime);
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