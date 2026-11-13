using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Forma de la zona de daño de cada ataque.
/// TipSphere = Zarpazo (esfera donde cae la punta de la espada, al frente del boss)
/// Fan       = Barrido (abanico ancho frente al boss)
/// Ring      = Pisotón (círculo alrededor del boss, el boss salta)
/// Charge    = Embestida (el boss avanza en línea recta hasta una zona marcada en el piso)
/// </summary>
public enum AttackShape { TipSphere, Fan, Ring, Charge }

/// <summary>
/// Datos de un ataque del boss. Se editan desde el Inspector.
/// </summary>
[System.Serializable]
public class BossAttack
{
    public string name = "Zarpazo";
    public AttackShape shape = AttackShape.TipSphere;
    public float damage = 25f;

    [Header("Cuándo se usa (distancia al jugador)")]
    public float minRange = 3f;
    public float maxRange = 7f;
    [Min(0.01f)] public float weight = 1f; // probabilidad relativa

    [Header("Tiempos (segundos)")]
    public float windup = 0.7f;    // aviso: el jugador puede esquivar
    public float active = 0.25f;   // ventana en la que sí daña
    public float recovery = 0.9f;  // el boss queda expuesto

    [Header("Zona de golpe")]
    public float reach = 2f;       // Fan: alcance del abanico. Charge: qué tan adelante está el círculo de daño
    public float radius = 1.2f;    // grosor de la zona de daño

    [Header("Solo TipSphere (Zarpazo)")]
    public float stepDistance = 5f; // cuánto avanza el boss mientras da el zarpazo (para alcanzarte a distancia larga)

    [Header("Solo Fan (Barrido)")]
    [Range(30f, 360f)] public float arcAngle = 150f; // apertura del abanico en grados

    [Header("Solo Charge (Embestida)")]
    public float lungeDistance = 22f; // cuánto avanza el boss (la zona marcada en el piso)
}

/// <summary>
/// IA de boss con máquina de estados: Idle, Chase, Attack, PhaseChange, Staggered y Dead.
/// Requiere CharacterController y Health en el mismo objeto.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Health))]
public class BossAI : MonoBehaviour
{
    private enum State { Idle, Chase, Attack, PhaseChange, Staggered, Dead }

    [Header("Referencias")]
    [SerializeField] private string bossName = "Gran Mutante";
    [SerializeField] private Transform player;
    [SerializeField] private Renderer bodyRenderer;

    [Header("Detección")]
    [SerializeField] private float aggroRange = 30f;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private float stoppingDistance = 2.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Ataques")]
    [SerializeField] private bool debugLog = true; // avisa en la Console cuando un ataque no te toca
    [SerializeField] private bool showAttackZones = true; // dibuja las zonas con Gizmos (activa el botón Gizmos)
    [SerializeField] private bool showChargeMarker = true; // dibuja en el piso, sin Gizmos, la zona a la que llegará la embestida
    [Header("Temblor de cámara (en todos los ataques, te peguen o no)")]
    [SerializeField] private bool attackShakeEnabled = true;
    [SerializeField, Min(0.05f)] private float attackShakeDuration = 0.3f;
    [SerializeField, Min(0f)] private float attackShakeMagnitude = 0.15f; // sube el valor para un temblor más fuerte

    [Header("Polvo y tierra al golpear")]
    [SerializeField] private bool dustEnabled = true;
    [SerializeField, Min(0.1f)] private float dustSizeMultiplier = 1f; // agranda o achica todas las nubes
    [SerializeField] private Color dustColor = new Color(0.55f, 0.43f, 0.31f, 0.75f);
    [SerializeField] private Color debrisColor = new Color(0.22f, 0.16f, 0.1f, 1f);

    [Header("Derribo del jugador")]
    [SerializeField] private bool knockdownOnCharge = true; // la Embestida te tira al suelo
    [SerializeField] private bool knockdownOnSweep = true;  // el Barrido te tira al suelo

    [Header("Zonas de golpe")]
    [SerializeField, Min(0.1f)] private float hitboxSizeMultiplier = 1.5f; // agranda la zona de todos los ataques
    [SerializeField] private float restTime = 0.5f; // pausa mínima entre ataques
    [SerializeField]
    private List<BossAttack> moveset = new List<BossAttack>
    {
        new BossAttack
        {
            name = "Pisotón", shape = AttackShape.Ring, damage = 30f,
            minRange = 0f, maxRange = 3f, windup = 1f, active = 0.25f, recovery = 1.2f,
            radius = 2.5f, stepDistance = 0f
        },
        new BossAttack
        {
            name = "Barrido", shape = AttackShape.Fan, damage = 28f,
            minRange = 3f, maxRange = 6f, windup = 0.8f, active = 0.5f, recovery = 1f,
            stepDistance = 0f,
            reach = 5f, radius = 1f, arcAngle = 150f
        },
        new BossAttack
        {
            name = "Zarpazo", shape = AttackShape.TipSphere, damage = 25f,
            minRange = 6f, maxRange = 10f, windup = 0.8f, active = 0.3f, recovery = 1f,
            radius = 1.2f, stepDistance = 5f
        },
        new BossAttack
        {
            name = "Embestida", shape = AttackShape.Charge, damage = 35f,
            minRange = 10f, maxRange = 25f, windup = 1f, active = 0.5f, recovery = 1.5f,
            reach = 4f, radius = 1.3f, lungeDistance = 22f
        }
    };

    [Header("Fase 2")]
    [SerializeField, Range(0.1f, 0.9f)] private float phase2HealthPercent = 0.5f;
    [SerializeField] private float phase2SpeedMultiplier = 1.3f;
    [SerializeField] private float phase2DamageMultiplier = 1.25f;
    [SerializeField] private float phase2WindupMultiplier = 0.75f; // más rápido = más difícil
    [SerializeField] private float phaseChangeDuration = 1.5f;

    [Header("Parry (aturdimiento)")]
    [SerializeField] private float staggerDuration = 2.5f;
    [SerializeField] private float riposteDamageMultiplier = 2f; // daño extra que recibe aturdido
    [SerializeField] private Color staggerColor = new Color(0.2f, 0.8f, 1f);

    [Header("Colores (feedback sin animaciones)")]
    [SerializeField] private Color phase2Color = new Color(0.5f, 0.05f, 0.05f);
    [SerializeField] private Color telegraphColor = Color.yellow;
    [SerializeField] private Color hitFlashColor = Color.white;

    [Header("Espada del boss")]
    [SerializeField] private Transform weaponPivot;   // Bone Blade Pivot
    [SerializeField] private Transform weaponTip;     // Bone Blade Tip (Empty en la punta de la hoja)

    [Header("Zarpazo (animación y zona en la punta)")]
    [SerializeField] private Vector3 raiseDirection = new Vector3(0f, 1f, -0.35f); // hacia dónde apunta la punta al levantar la espada (x = derecha, y = arriba, z = frente)
    [SerializeField, Range(-0.9f, 0.5f)] private float zarpazoDrop = -0.3f; // qué tan abajo cae la punta (más negativo = cae más cerca del boss)

    [Header("Barrido (animación de la espada)")]
    [SerializeField] private Vector3 slashStartDirection = new Vector3(1f, 1f, 0f); // hacia dónde apunta la punta al preparar el corte
    [SerializeField, Range(-0.9f, 0.5f)] private float slashEndDrop = -0.3f; // qué tan abajo termina la punta
    [SerializeField, Range(0f, 1f)] private float slashEndOvershoot = 0.8f; // 0 = termina en el centro, 1 = casi al lado opuesto
    [SerializeField] private bool mirrorSlash = false; // marca esto si el corte sale del lado equivocado
    [SerializeField] private bool showPreview = true; // muestra los ataques en la vista Scene al seleccionar al boss, sin Play

    private CharacterController controller;
    private Health health;
    private Health playerHealth;
    private PlayerShield playerShield;
    private PlayerHitFeedback playerFeedback;
    private PlayerController playerMovement;
    private ThirdPersonCamera cameraShaker;
    private float playerRadius = 0.5f;
    private bool weaponAnimationEnabled;
    private Material material;
    private Color phase1Color;

    // Pose de reposo de la espada, guardada al empezar el juego.
    private Quaternion restRotation = Quaternion.identity;
    private Vector3 restPivotLocalPos;
    private Vector3 restTipOffset; // del pivote a la punta, en el espacio del padre
    private bool bladeReady;
    private Quaternion poseStart = Quaternion.identity;
    private Quaternion poseEnd = Quaternion.identity;
    private Vector3 strikeFromDir = Vector3.up;   // hacia dónde apunta la punta al empezar el golpe (espacio del boss)
    private Vector3 strikeToDir = Vector3.forward; // y al terminarlo

    // Marcadores en el piso de la embestida.
    private GameObject chargeStrip;
    private GameObject chargeDisc;
    private Vector3 chargeDir = Vector3.forward;

    private State state = State.Idle;
    private int phase = 1;
    private float verticalVelocity;
    private float nextAttackTime;
    private float nextNoAttackLog;
    private bool telegraphing;
    private bool hitboxActive;
    private bool playerHitThisAttack;
    private BossAttack currentAttack;
    private Coroutine flashRoutine;

    public string BossName => bossName;
    public Health Health => health;
    public bool IsEngaged { get; private set; }

    private float SpeedMultiplier => phase == 2 ? phase2SpeedMultiplier : 1f;
    private Color BaseColor => state == State.Staggered ? staggerColor : (phase == 2 ? phase2Color : phase1Color);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null)
        {
            material = bodyRenderer.material; // instancia propia
            phase1Color = material.color;
        }

        CaptureRest();
    }

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("BossAI: asigna al jugador en el campo Player.", this);
            enabled = false;
            return;
        }

        playerHealth = player.GetComponent<Health>();
        playerShield = player.GetComponent<PlayerShield>();
        playerFeedback = player.GetComponent<PlayerHitFeedback>();
        playerMovement = player.GetComponent<PlayerController>();
        if (Camera.main != null) cameraShaker = Camera.main.GetComponent<ThirdPersonCamera>();

        CharacterController playerController = player.GetComponent<CharacterController>();
        if (playerController != null)
            playerRadius = playerController.radius * Mathf.Max(player.lossyScale.x, player.lossyScale.z);

        if (playerHealth == null)
            Debug.LogError("BossAI: el objeto del campo Player no tiene el componente Health. " +
                           "Arrastra al objeto raíz del jugador (ROACH), no a un hijo.", this);
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void OnDestroy()
    {
        HideChargeMarker(true);
    }

    private void Update()
    {
        if (state == State.Dead) return;

        switch (state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Chase: UpdateChase(); break;
                // Attack y PhaseChange los controlan sus corrutinas.
        }

        ApplyGravity();
    }

    // ---------- Estados ----------

    private void UpdateIdle()
    {
        if (playerHealth != null && playerHealth.IsDead) return;

        if (FlatDirectionToPlayer().magnitude <= aggroRange)
        {
            IsEngaged = true;
            state = State.Chase;
        }
    }

    private void UpdateChase()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            IsEngaged = false;
            state = State.Idle;
            return;
        }

        if (phase == 1 && health.Current <= health.Max * phase2HealthPercent)
        {
            StartCoroutine(PhaseChangeRoutine());
            return;
        }

        Vector3 toPlayer = FlatDirectionToPlayer();
        float distance = toPlayer.magnitude;
        Vector3 direction = toPlayer.normalized;

        Face(direction, turnSpeed);

        BossAttack next = ChooseAttack(distance);
        if (next != null)
        {
            if (debugLog)
                Debug.Log("[BossAI] elige '" + next.name + "' a distancia " + distance.ToString("F1"), this);
            StartCoroutine(AttackRoutine(next));
            return;
        }

        if (debugLog && Time.time >= nextAttackTime && Time.time >= nextNoAttackLog)
        {
            nextNoAttackLog = Time.time + 1.5f;
            Debug.Log("[BossAI] ningún ataque sirve a distancia " + distance.ToString("F1") +
                      ". Revisa Min Range y Max Range en la lista Moveset.", this);
        }

        if (distance > stoppingDistance)
            controller.Move(direction * moveSpeed * SpeedMultiplier * Time.deltaTime);
    }

    private BossAttack ChooseAttack(float distance)
    {
        if (Time.time < nextAttackTime) return null;

        float totalWeight = 0f;
        foreach (BossAttack a in moveset)
            if (distance >= a.minRange && distance <= a.maxRange) totalWeight += a.weight;

        if (totalWeight <= 0f) return null;

        float roll = Random.value * totalWeight;
        foreach (BossAttack a in moveset)
        {
            if (distance < a.minRange || distance > a.maxRange) continue;
            roll -= a.weight;
            if (roll <= 0f) return a;
        }

        return null;
    }

    // ---------- Corrutinas ----------

    private IEnumerator AttackRoutine(BossAttack attack)
    {
        state = State.Attack;
        currentAttack = attack;
        playerHitThisAttack = false;

        bool enraged = phase == 2;
        float windup = attack.windup * (enraged ? phase2WindupMultiplier : 1f);
        float damage = attack.damage * (enraged ? phase2DamageMultiplier : 1f);

        // Solo el Zarpazo y el Barrido mueven la espada.
        weaponAnimationEnabled = bladeReady && (attack.shape == AttackShape.TipSphere || attack.shape == AttackShape.Fan);
        if (weaponAnimationEnabled)
            GetPoses(attack, out poseStart, out poseEnd);

        // El pisotón salta durante el aviso y cae justo cuando empieza el golpe.
        if (attack.shape == AttackShape.Ring)
            verticalVelocity = -gravity * windup * 0.5f;

        // 1) Aviso: se pone amarillo y sigue al jugador, pero deja de girar al final
        //    para que el jugador pueda esquivar hacia un lado.
        telegraphing = true;
        float t = 0f;
        while (t < windup)
        {
            t += Time.deltaTime;
            PoseWindup(t / Mathf.Max(windup, 0.01f));
            SetColor(telegraphColor);
            if (t < windup * 0.6f) Face(FlatDirectionToPlayer().normalized, turnSpeed);
            if (attack.shape == AttackShape.Charge) UpdateChargeMarker(attack, false);
            yield return null;
        }
        telegraphing = false;
        SetColor(BaseColor);

        // 2) Golpe: la zona de daño está activa.
        hitboxActive = true;
        float active = Mathf.Max(attack.active, 0.01f);

        // El pisotón levanta polvo en cuanto cae.
        if (attack.shape == AttackShape.Ring)
        {
            ShakeCamera();
            SpawnImpactDust(attack);
        }
        float trailTimer = 0f;
        if (attack.shape == AttackShape.Charge)
        {
            chargeDir = FlatForward();
            UpdateChargeMarker(attack, true);
        }

        t = 0f;
        while (t < attack.active)
        {
            t += Time.deltaTime;
            PoseStrike(t / active);

            if (attack.shape == AttackShape.Charge)
                controller.Move(chargeDir * (attack.lungeDistance / active) * Time.deltaTime);
            else if (attack.shape == AttackShape.TipSphere && attack.stepDistance > 0f)
                controller.Move(FlatForward() * (attack.stepDistance / active) * Time.deltaTime);

            // Rastro de polvo detrás del boss mientras embiste.
            if (attack.shape == AttackShape.Charge && dustEnabled)
            {
                trailTimer -= Time.deltaTime;
                if (trailTimer <= 0f)
                {
                    trailTimer = 0.07f;
                    SpawnDustAt(transform.position, 1.5f, 1.2f, 4, false);
                }
            }

            DoHitCheck(attack, damage);
            yield return null;
        }
        hitboxActive = false;
        HideChargeMarker(false);

        // Zarpazo, barrido y final de la embestida levantan polvo al terminar el golpe.
        if (attack.shape != AttackShape.Ring)
        {
            ShakeCamera();
            SpawnImpactDust(attack);
        }

        if (!playerHitThisAttack && debugLog) LogMiss(attack);

        // 3) Recuperación: el boss queda quieto y vulnerable.
        // La espada vuelve despacio a su posición de reposo.
        t = 0f;
        float recovery = Mathf.Max(attack.recovery, 0.01f);
        while (t < recovery)
        {
            t += Time.deltaTime;
            PoseRecover(t / recovery);
            yield return null;
        }
        ResetPose();

        nextAttackTime = Time.time + restTime;
        currentAttack = null;
        state = State.Chase;
    }

    private IEnumerator PhaseChangeRoutine()
    {
        state = State.PhaseChange;
        phase = 2;
        SetColor(phase2Color);

        // Invulnerable mientras ruge, para que no lo maten en plena transición.
        health.Invulnerable = true;
        yield return new WaitForSeconds(phaseChangeDuration);
        health.Invulnerable = false;

        nextAttackTime = Time.time + restTime;
        state = State.Chase;
    }

    // ---------- Daño ----------

    private void DoHitCheck(BossAttack attack, float damage)
    {
        if (playerHitThisAttack || playerHealth == null) return;
        if (!IsPlayerInside(attack)) return;

        playerHitThisAttack = true;

        // El escudo decide si el golpe entra, se bloquea o se parrea.
        float finalDamage = damage;
        HitOutcome outcome = HitOutcome.Hit;
        if (playerShield != null)
            outcome = playerShield.ResolveHit(damage, transform, out finalDamage);

        bool damageApplied = false;
        if (finalDamage > 0f)
            damageApplied = playerHealth.TakeDamage(finalDamage); // respeta los i-frames de la esquiva

        // La Embestida y el Barrido derriban al jugador (solo si el golpe sí entró, no si lo bloqueas).
        bool knocksDown = (attack.shape == AttackShape.Charge && knockdownOnCharge)
                          || (attack.shape == AttackShape.Fan && knockdownOnSweep);
        if (damageApplied && knocksDown && outcome != HitOutcome.Blocked && playerMovement != null)
            playerMovement.StartKnockdown(transform.position);

        if (outcome == HitOutcome.Parried) Stagger();
    }

    private float EffectiveRadius(BossAttack attack)
    {
        return attack.radius * hitboxSizeMultiplier;
    }

    // Solo cuenta la distancia horizontal: el centro del boss queda más alto que el jugador
    // y una esfera 3D pasaría por encima de él sin tocarlo.
    private bool IsPlayerInside(BossAttack attack)
    {
        if (attack.shape == AttackShape.Fan) return IsInsideFan(attack);

        Vector3 toPlayer = player.position - GetHitCenter(attack);
        toPlayer.y = 0f;
        return toPlayer.magnitude <= EffectiveRadius(attack) + playerRadius;
    }

    // Centro de la esfera de daño (a la altura del jugador). No aplica al abanico.
    private Vector3 GetHitCenter(BossAttack attack)
    {
        Vector3 center;

        switch (attack.shape)
        {
            case AttackShape.Ring:
                center = transform.position;
                break;

            case AttackShape.Charge:
                center = transform.position + FlatForward() * attack.reach;
                break;

            default: // TipSphere: donde cae la punta al terminar el golpe
                if (bladeReady)
                    center = TipWorldForPose(AimPose(GetCrossDirection(zarpazoDrop, 0f)));
                else
                    center = transform.position + FlatForward() * attack.reach;
                break;
        }

        center.y = player != null ? player.position.y : transform.position.y;
        return center;
    }

    // ---------- Abanico (Barrido) ----------

    private float FanRange(BossAttack attack)
    {
        return attack.reach + EffectiveRadius(attack);
    }

    private bool IsInsideFan(BossAttack attack)
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        if (distance > FanRange(attack) + playerRadius) return false;
        if (distance < 0.1f) return true;

        return Vector3.Angle(FlatForward(), toPlayer) <= attack.arcAngle * 0.5f;
    }

    private void LogMiss(BossAttack attack)
    {
        float distance;
        float needed;

        if (attack.shape == AttackShape.Fan)
        {
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            distance = d.magnitude;
            needed = FanRange(attack) + playerRadius;
        }
        else
        {
            Vector3 d = player.position - GetHitCenter(attack);
            d.y = 0f;
            distance = d.magnitude;
            needed = EffectiveRadius(attack) + playerRadius;
        }

        Debug.Log("[BossAI] '" + attack.name + "' no tocó al jugador: distancia " + distance.ToString("F1") +
                  ", debe ser menor a " + needed.ToString("F1") +
                  (attack.shape == AttackShape.Fan ? " y estar dentro del ángulo del abanico" : "") +
                  ". Sube Reach y Radius de ese ataque.", this);
    }

    // ---------- Poses de la espada ----------

    // Guarda cómo está la espada en reposo (para animarla y volver a esa pose).
    private void CaptureRest()
    {
        bladeReady = false;
        if (weaponPivot == null) return;

        restRotation = weaponPivot.localRotation;
        restPivotLocalPos = weaponPivot.localPosition;

        if (weaponTip == null) return;

        Transform parent = weaponPivot.parent;
        Vector3 tipInParent = parent != null ? parent.InverseTransformPoint(weaponTip.position) : weaponTip.position;
        restTipOffset = tipInParent - restPivotLocalPos;
        bladeReady = restTipOffset.sqrMagnitude > 0.0001f;
    }

    // Rotación del pivote para que la punta apunte hacia "directionBoss" (en el espacio local del boss).
    // Funciona sin importar hacia dónde apunte el modelo en reposo.
    private Quaternion AimPose(Vector3 directionBoss)
    {
        Vector3 world = transform.TransformDirection(directionBoss);
        Vector3 inParent = weaponPivot.parent != null ? weaponPivot.parent.InverseTransformDirection(world) : world;
        if (inParent.sqrMagnitude < 0.0001f) return restRotation;

        Quaternion delta = Quaternion.FromToRotation(restTipOffset.normalized, inParent.normalized);
        return delta * restRotation;
    }

    // Posición en el mundo de la punta si el pivote tuviera esa rotación.
    private Vector3 TipWorldForPose(Quaternion pose)
    {
        Quaternion delta = pose * Quaternion.Inverse(restRotation);
        Vector3 local = restPivotLocalPos + delta * restTipOffset;
        return weaponPivot.parent != null ? weaponPivot.parent.TransformPoint(local) : local;
    }

    // Lado de la mano: +1 si el pivote está a la derecha del boss, -1 si está a la izquierda.
    private float HandSide()
    {
        float side = transform.InverseTransformPoint(weaponPivot.position).x >= 0f ? 1f : -1f;
        return mirrorSlash ? -side : side;
    }

    // Dirección (espacio local del boss) para que la punta caiga al frente del boss.
    // overshoot 0 = sobre la línea central, 1 = casi al lado opuesto de la mano.
    private Vector3 GetCrossDirection(float drop, float overshoot)
    {
        Vector3 pivotBoss = transform.InverseTransformPoint(weaponPivot.position);
        float length = (transform.InverseTransformPoint(weaponTip.position) - pivotBoss).magnitude;

        float y = drop * length;
        float flat = Mathf.Sqrt(Mathf.Max(length * length - y * y, 0f)); // largo de la hoja sobre el suelo

        float targetX = -HandSide() * overshoot * flat;                  // posición lateral de la punta (0 = centro)
        float sideways = Mathf.Clamp(targetX - pivotBoss.x, -flat * 0.95f, flat * 0.95f);
        float forward = Mathf.Sqrt(Mathf.Max(flat * flat - sideways * sideways, 0f));
        return new Vector3(sideways, y, forward);
    }

    private Vector3 GetSlashStartDirection()
    {
        Vector3 d = slashStartDirection;
        d.x = Mathf.Abs(d.x) * HandSide();
        return d;
    }

    // Poses de inicio y fin de la espada según el ataque.
    private void GetPoses(BossAttack attack, out Quaternion start, out Quaternion end)
    {
        if (attack.shape == AttackShape.Fan)
        {
            strikeFromDir = GetSlashStartDirection();
            strikeToDir = GetCrossDirection(slashEndDrop, slashEndOvershoot);
        }
        else
        {
            strikeFromDir = raiseDirection;
            strikeToDir = GetCrossDirection(zarpazoDrop, 0f);
        }

        start = AimPose(strikeFromDir);
        end = AimPose(strikeToDir);
    }

    private void PoseWindup(float k)
    {
        k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        SetPose(Quaternion.Slerp(restRotation, poseStart, k));
    }

    private void PoseStrike(float k)
    {
        k = Mathf.Clamp01(k);
        if (!weaponAnimationEnabled) return;

        if (currentAttack != null && currentAttack.shape == AttackShape.Fan)
        {
            // Barrido: visto de frente, la punta sube y baja en línea recta (diagonal).
            // Se interpolan solo derecha/izquierda y arriba/abajo; el frente se calcula para mantener el largo de la hoja.
            Vector3 a = strikeFromDir.normalized;
            Vector3 b = strikeToDir.normalized;
            float x = Mathf.Lerp(a.x, b.x, k);
            float y = Mathf.Lerp(a.y, b.y, k);
            float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
            SetPose(AimPose(new Vector3(x, y, z)));
        }
        else
        {
            // Zarpazo: arco directo de arriba hacia abajo.
            SetPose(AimPose(Vector3.Slerp(strikeFromDir, strikeToDir, k)));
        }
    }

    private void PoseRecover(float k)
    {
        SetPose(Quaternion.Slerp(poseEnd, restRotation, Mathf.Clamp01(k)));
    }

    private void SetPose(Quaternion pose)
    {
        if (weaponPivot != null && weaponAnimationEnabled) weaponPivot.localRotation = pose;
    }

    private void ResetPose()
    {
        if (weaponPivot != null && weaponAnimationEnabled) weaponPivot.localRotation = restRotation;
    }

    // ---------- Marcador de la embestida (en el piso, visible sin Gizmos) ----------

    // Dibuja la franja por la que pasará la zona de daño y un círculo donde termina.
    private void UpdateChargeMarker(BossAttack attack, bool striking)
    {
        if (!showChargeMarker) return;

        Vector3 dir = striking ? chargeDir : FlatForward();
        float r = EffectiveRadius(attack) + playerRadius;

        Vector3 start = transform.position + dir * attack.reach;
        Vector3 end = start + dir * attack.lungeDistance;
        float y = GroundY(end) + 0.05f;
        start.y = y;
        end.y = y;

        if (chargeStrip == null) chargeStrip = CreateMarker(PrimitiveType.Cube);
        if (chargeDisc == null) chargeDisc = CreateMarker(PrimitiveType.Cylinder);

        Color color = striking ? new Color(1f, 0.1f, 0.05f, 0.45f) : new Color(1f, 0.9f, 0.1f, 0.35f);
        SetMarkerColor(chargeStrip, color);
        SetMarkerColor(chargeDisc, color);

        Vector3 middle = (start + end) * 0.5f;
        chargeStrip.transform.SetPositionAndRotation(middle, Quaternion.LookRotation(dir));
        chargeStrip.transform.localScale = new Vector3(r * 2f, 0.02f, attack.lungeDistance);

        chargeDisc.transform.SetPositionAndRotation(end, Quaternion.identity);
        chargeDisc.transform.localScale = new Vector3(r * 2.4f, 0.01f, r * 2.4f);
    }

    private void HideChargeMarker(bool destroy)
    {
        if (destroy)
        {
            if (chargeStrip != null) Destroy(chargeStrip);
            if (chargeDisc != null) Destroy(chargeDisc);
            chargeStrip = null;
            chargeDisc = null;
            return;
        }

        if (chargeStrip != null) chargeStrip.SetActive(false);
        if (chargeDisc != null) chargeDisc.SetActive(false);
    }

    private GameObject CreateMarker(PrimitiveType type)
    {
        GameObject marker = GameObject.CreatePrimitive(type);
        marker.name = "ChargeMarker";

        Collider col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = marker.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        return marker;
    }

    private static void SetMarkerColor(GameObject marker, Color color)
    {
        marker.SetActive(true);
        marker.GetComponent<Renderer>().material.color = color;
    }

    // Altura del piso bajo un punto (ignora al boss y al jugador).
    private float GroundY(Vector3 point)
    {
        float fallback = player != null ? player.position.y : transform.position.y;
        Vector3 origin = new Vector3(point.x, Mathf.Max(transform.position.y, fallback) + 5f, point.z);

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        float groundY = fallback;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player)) continue;
            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                groundY = hit.point.y;
            }
        }

        return groundY;
    }

    // ---------- Temblor, polvo y tierra ----------

    // Cada ataque hace temblar la pantalla cuando cae, aunque el jugador esté lejos o lo haya esquivado.
    private void ShakeCamera()
    {
        if (attackShakeEnabled && cameraShaker != null)
            cameraShaker.Shake(attackShakeDuration, attackShakeMagnitude);
    }

    private void SpawnImpactDust(BossAttack attack)
    {
        if (!dustEnabled) return;

        float r = EffectiveRadius(attack);

        switch (attack.shape)
        {
            case AttackShape.Ring:
                SpawnDustAt(transform.position, r * 0.9f, 1.4f, 30, true);
                break;

            case AttackShape.TipSphere:
                SpawnDustAt(GetHitCenter(attack), r * 0.6f, 1.1f, 18, true);
                break;

            case AttackShape.Fan:
                float half = attack.arcAngle * 0.5f * 0.8f;
                for (int i = 0; i < 5; i++)
                {
                    float angle = Mathf.Lerp(-half, half, i / 4f);
                    Vector3 point = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * FlatForward() * attack.reach * 0.8f;
                    SpawnDustAt(point, 1.2f, 1f, 10, i % 2 == 0);
                }
                break;

            case AttackShape.Charge:
                SpawnDustAt(transform.position, r * 0.8f, 1.6f, 35, true);
                break;
        }
    }

    private void SpawnDustAt(Vector3 position, float spread, float size, int count, bool debris)
    {
        position.y = GroundY(position) + 0.05f;
        ImpactDust.Burst(position, spread, size * dustSizeMultiplier, count, dustColor, debrisColor, debris);
    }

    // ---------- Aturdimiento, daño recibido, muerte ----------

    // Un parry interrumpe el ataque: el boss queda aturdido y recibe más daño.
    private void Stagger()
    {
        if (state == State.Dead) return;

        StopAllCoroutines();
        flashRoutine = null;
        ResetPose();
        HideChargeMarker(false);
        StartCoroutine(StaggerRoutine());
    }

    private IEnumerator StaggerRoutine()
    {
        state = State.Staggered;
        telegraphing = false;
        hitboxActive = false;
        currentAttack = null;

        SetColor(staggerColor);
        health.DamageTakenMultiplier = riposteDamageMultiplier;

        yield return new WaitForSeconds(staggerDuration);

        health.DamageTakenMultiplier = 1f;
        nextAttackTime = Time.time + restTime;
        state = State.Chase;
        SetColor(BaseColor);
    }

    private void OnDamaged(float amount)
    {
        if (telegraphing || state == State.Dead) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        SetColor(hitFlashColor);
        yield return new WaitForSeconds(0.08f);
        SetColor(BaseColor);
        flashRoutine = null;
    }

    private void OnDied()
    {
        state = State.Dead;
        IsEngaged = false;
        hitboxActive = false;
        StopAllCoroutines();
        HideChargeMarker(true);
    }

    // ---------- Utilidades ----------

    private Vector3 FlatDirectionToPlayer()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d;
    }

    private Vector3 FlatForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    private void Face(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, speed * Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void SetColor(Color color)
    {
        if (material != null) material.color = color;
    }

    // ---------- Gizmos ----------

    private void DrawFanGizmo(BossAttack attack, Color color, bool filled)
    {
        float y = player != null ? player.position.y : transform.position.y;
        Vector3 origin = transform.position;
        origin.y = y;

        Vector3 forward = FlatForward();
        float half = attack.arcAngle * 0.5f;
        float range = FanRange(attack) + playerRadius;
        const int steps = 16;

        Gizmos.color = color;
        Vector3 previous = origin + Quaternion.AngleAxis(-half, Vector3.up) * forward * range;
        Gizmos.DrawLine(origin, previous);
        for (int i = 1; i <= steps; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)steps);
            Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * range;
            Gizmos.DrawLine(previous, point);
            if (filled) Gizmos.DrawLine(origin, point);
            previous = point;
        }
        Gizmos.DrawLine(previous, origin);
    }

    // Línea de la zona de la embestida (inicio y final) con esfera de daño en ambos extremos.
    private void DrawChargeGizmo(BossAttack attack, Color color)
    {
        Vector3 dir = FlatForward();
        Vector3 start = GetHitCenter(attack);
        Vector3 end = start + dir * attack.lungeDistance;

        Gizmos.color = color;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, EffectiveRadius(attack));
        Gizmos.DrawWireSphere(end, EffectiveRadius(attack));
    }

    // Amarillo = aviso, rojo = el golpe daña ahora (activa Gizmos en la vista Game).
    private void OnDrawGizmos()
    {
        if (!showAttackZones || state != State.Attack || currentAttack == null) return;

        switch (currentAttack.shape)
        {
            case AttackShape.Fan:
                DrawFanGizmo(currentAttack, hitboxActive ? new Color(1f, 0f, 0f, 0.8f) : Color.yellow, hitboxActive);
                break;

            case AttackShape.Charge:
                DrawChargeGizmo(currentAttack, hitboxActive ? Color.red : Color.yellow);
                if (hitboxActive)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                    Gizmos.DrawSphere(GetHitCenter(currentAttack), EffectiveRadius(currentAttack));
                }
                break;

            default: // TipSphere y Ring
                Vector3 center = GetHitCenter(currentAttack);
                if (!hitboxActive && currentAttack.shape == AttackShape.TipSphere)
                    center += FlatForward() * currentAttack.stepDistance; // donde quedará al terminar el avance
                if (hitboxActive)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                    Gizmos.DrawSphere(center, EffectiveRadius(currentAttack));
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(center, EffectiveRadius(currentAttack));
                }
                break;
        }
    }

    // Vista previa de todos los ataques en la vista Scene, con el boss seleccionado y sin dar Play.
    // Déjalo con la espada en su pose de reposo (el pivote sin girar).
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying || !showPreview || weaponPivot == null || weaponTip == null) return;

        CaptureRest();

        Color preview = new Color(0f, 0.9f, 1f);
        foreach (BossAttack attack in moveset)
        {
            Gizmos.color = preview;

            switch (attack.shape)
            {
                case AttackShape.Fan:
                    DrawFanGizmo(attack, preview, false);
                    break;

                case AttackShape.Charge:
                    DrawChargeGizmo(attack, preview);
                    break;

                case AttackShape.Ring:
                    Gizmos.DrawWireSphere(GetHitCenter(attack), EffectiveRadius(attack));
                    break;

                default: // TipSphere
                    if (bladeReady)
                    {
                        Vector3 start = GetHitCenter(attack);
                        Vector3 end = start + FlatForward() * attack.stepDistance;
                        Gizmos.DrawWireSphere(start, EffectiveRadius(attack));
                        Gizmos.DrawLine(start, end);
                        Gizmos.DrawWireSphere(end, EffectiveRadius(attack));
                    }
                    break;
            }
        }
    }
}