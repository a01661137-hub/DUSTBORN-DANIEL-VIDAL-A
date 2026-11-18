using UnityEngine;

/// <summary>
/// Pasa la velocidad real del personaje al Animator, para que la animación de caminar
/// se active cuando avanza y se detenga cuando se queda quieto.
/// Ponlo en ROACH (el objeto raíz que tiene PlayerController).
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator; // el Animator del modelo (si lo dejas vacío, lo busca en los hijos)

    [Header("Parámetro del Animator")]
    [SerializeField] private string speedParameter = "Speed"; // nombre del parámetro Float en el Animator
    [SerializeField] private float dampTime = 0.1f;           // suaviza el cambio entre quieto y caminando

    [Header("Cámara fijada (lock-on): pose de pelea")]
    [Tooltip("Estado del Animator para la pose de pelea quieto.")]
    [SerializeField] private string stanceIdleState = "StanceIdle";
    [SerializeField] private string stanceForwardState = "StanceForward";
    [SerializeField] private string stanceBackState = "StanceBack";
    [SerializeField] private string stanceLeftState = "StanceLeft";
    [SerializeField] private string stanceRightState = "StanceRight";
    [Tooltip("Estado al que se vuelve al soltar la cámara (tu Blend Tree de caminar y correr).")]
    [SerializeField] private string locomotionStateName = "Locomotion";
    [SerializeField] private float stanceCrossFade = 0.15f;
    [Tooltip("Duración de la mezcla al terminar un roll, un derribo o levantarte (reemplaza la Transition Duration de las flechas de salida).")]
    [SerializeField] private float exitCrossFade = 0.25f;
    [Tooltip("Mezcla al terminar SOLO el roll de caminar (hacia idle, caminar o la guardia). Más bajo = transición más rápida.")]
    [SerializeField] private float rollWalkExitCrossFade = 0.12f;
    [Tooltip("Déjalo apagado si tu StanceIdle es una animación idle real que se repite. Enciéndelo solo para congelar una pose.")]
    [SerializeField] private bool freezeStanceIdle = false;
    [Tooltip("Velocidad de reproducción al caminar con la cámara fijada (adelante, atrás y lados). 1 = normal, más = más rápido.")]
    [SerializeField] private float stanceMoveAnimSpeed = 1.4f;
    [Tooltip("Tiempo mínimo antes de cambiar de dirección, para que no parpadee entre animaciones.")]
    [SerializeField] private float stanceMinHold = 0.15f;

    [Header("Si NO tienes animación de quieto (solo caminar)")]
    [SerializeField] private bool freezeWhenStopped = false;  // congela la pose cuando no te mueves

    [Header("Velocidad de la animación (con Blend Tree de caminar y correr)")]
    [SerializeField] private float walkAnimSpeed = 1f;     // velocidad de la animación al caminar (1 = normal, menos = más lenta)
    [SerializeField] private float runAnimSpeed = 0.7f;    // velocidad de la animación al correr (baja el número si se ve muy rápida)
    [SerializeField] private float walkSpeedReference = 3f; // tu Walk Speed del PlayerController
    [SerializeField] private float runSpeedReference = 6f;  // tu Run Speed del PlayerController

    [Header("Si solo tienes animación de caminar")]
    [SerializeField] private bool scalePlaybackWithSpeed = false; // acelera la animación de caminar al correr
    [SerializeField] private float referenceSpeed = 3f;          // velocidad a la que la animación se ve natural (tu Walk Speed)
    [SerializeField] private float maxPlaybackSpeed = 1.8f;

    [Header("Esquivar (Roll)")]
    [SerializeField] private string rollWalkStateName = "RollWalk"; // estado del Animator para rodar caminando
    [SerializeField] private string rollRunStateName = "RollRun";   // estado del Animator para rodar corriendo
    [SerializeField] private string rollRunAltStateName = "RollRunAlt"; // estado del Animator para el barrido
    [SerializeField] private float rollCrossFade = 0.08f;
    [Tooltip("Grados que se gira el modelo poco a poco durante el roll caminando para compensar el giro del clip. Si el clip gira a la izquierda, prueba valores positivos (a la derecha); si se pasa, negativos.")]
    [SerializeField] private float rollWalkYawCompensation = 0f;
    [Tooltip("Lo mismo para el roll corriendo.")]
    [SerializeField] private float rollRunYawCompensation = 0f;
    [Tooltip("Compensación de giro para el barrido.")]
    [SerializeField] private float rollRunAltYawCompensation = 0f;
    [Tooltip("Segundos que tarda en soltarse la compensación al terminar el roll. Iguala la Transition Duration de la flecha RollWalk > Locomotion (0.15 a 0.25).")]
    [SerializeField] private float rollYawReleaseTime = 0.2f;

    [Header("Cámara fijada: esquive hacia atrás y de lado")]
    [SerializeField] private string rollBackStateName = "RollBack";
    [SerializeField] private string rollSideLeftStateName = "RollSideLeft";
    [SerializeField] private string rollSideRightStateName = "RollSideRight";

    [Header("Ataque")]
    [SerializeField] private string attackLightStateName = "AttackLight";
    [Tooltip("Si no existe este estado, el ataque pesado usa la animación del ligero.")]
    [SerializeField] private string attackHeavyStateName = "AttackHeavy";
    [SerializeField] private float attackCrossFade = 0.08f;
    [Tooltip("Mezcla al terminar el ataque hacia caminar o la guardia.")]
    [SerializeField] private float attackExitCrossFade = 0.15f;

    [Header("Derribo (Embestida y Barrido del jefe)")]
    [SerializeField] private string knockdownStateName = "Knockdown"; // nombre del estado en el Animator
    [SerializeField] private float knockdownCrossFade = 0.05f;
    [SerializeField] private string getUpStateName = "GetUp"; // estado de la animación de levantarse
    [SerializeField] private float getUpCrossFade = 0.1f;

    [Header("Diagnóstico")]
    [Tooltip("Escribe en la Console qué animación se reproduce al rodar. Apágalo cuando todo funcione.")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private float measuredSpeed;   // velocidad real de ROACH en metros por segundo
    [SerializeField] private float appliedAnimSpeed; // velocidad de reproducción que se le está dando a la animación

    private int speedHash;
    private bool hasSpeedParameter;
    private int defaultStateHash; // estado inicial del Animator (tu Locomotion), por si el nombre no coincide
    private string currentRollStateName;
    private bool currentRollIsLocked;
    private float exitFade = 0.25f; // mezcla que se usa al salir del estado especial que acaba de terminar
    private PlayerCombat combat;
    private int lastAttackSerial;
    private float attackClipSpeed = 1f;
    private string currentAttackStateName;
    private bool exitPending;    // se acaba de terminar un roll/derribo: hay que volver a la guardia o a Locomotion
    private enum StanceState { None, Idle, Forward, Back, Left, Right }
    private StanceState stance = StanceState.None;
    private float stanceChangedAt;
    private Vector3 lastPosition;

    private PlayerController player;
    private bool wasRolling;
    private bool wasKnockedDown;
    private bool wasGettingUp;
    private float rollClipSpeed = 1f;
    private float knockdownClipSpeed = 1f;
    private float getUpClipSpeed = 1f;

    // Compensación de giro durante el roll.
    private Transform modelTransform;
    private Quaternion modelBaseRotation = Quaternion.identity;
    private float currentYawCompensation;
    private float rollStartTime;
    private float rollTotalTime = 1f;
    private bool yawApplied;
    private float appliedYaw;
    private float yawReleaseStart = -1f;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        combat = GetComponent<PlayerCombat>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("PlayerAnimator: no encontré ningún Animator en ROACH ni en sus hijos. " +
                             "Agrega el componente Animator al modelo o arrástralo al campo Animator.", this);
            enabled = false;
            return;
        }

        // El movimiento lo hace PlayerController, no la animación.
        animator.applyRootMotion = false;

        ValidateStates();

        // Solo se rota el modelo (el hijo con el Animator), nunca el objeto raíz ROACH.
        if (animator.transform != transform)
        {
            modelTransform = animator.transform;
            modelBaseRotation = modelTransform.localRotation;
        }

        speedHash = Animator.StringToHash(speedParameter);
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedParameter = true;
                break;
            }
        }

        if (!hasSpeedParameter)
        {
            Debug.LogWarning("PlayerAnimator: el Animator no tiene un parámetro Float llamado '" + speedParameter +
                             "'. Créalo en la ventana Animator > Parameters.", this);
        }

        lastPosition = transform.position;
    }

    // Se calcula con el cambio de posición, porque CharacterController.velocity
    // no sirve aquí (PlayerController mueve horizontal y gravedad en llamadas separadas).
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // El primer estado que se reproduce es el inicial del Animator (Locomotion).
        if (defaultStateHash == 0)
            defaultStateHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float speed = delta.magnitude / dt;
        if (speed < 0.05f) speed = 0f; // evita movimientos fantasma por gravedad o choques

        // Derribo: al empezar se reproduce su animación, ajustada a la duración del derribo.
        bool knockedDown = player != null && player.IsKnockedDown;
        if (knockedDown && !wasKnockedDown)
        {
            // Si había compensación de giro de un roll, se quita de golpe.
            if (modelTransform != null && yawApplied)
            {
                modelTransform.localRotation = modelBaseRotation;
                yawApplied = false;
                yawReleaseStart = -1f;
            }

            animator.CrossFadeInFixedTime(knockdownStateName, knockdownCrossFade, 0, 0f);
            knockdownClipSpeed = MeasureClipSpeed(player.KnockdownDuration);
        }
        wasKnockedDown = knockedDown;

        // Fase 2: al empezar a levantarse se pasa a la animación de levantarse.
        bool gettingUp = knockedDown && player.IsGettingUp;
        if (gettingUp && !wasGettingUp)
        {
            animator.CrossFadeInFixedTime(getUpStateName, getUpCrossFade, 0, 0f);
            getUpClipSpeed = MeasureClipSpeed(player.GetUpDuration);
        }
        wasGettingUp = gettingUp;

        if (knockedDown)
        {
            stance = StanceState.None; // al terminar se vuelve a elegir la pose de pelea
            exitPending = true;
            exitFade = exitCrossFade;
            if (gettingUp) HoldState(getUpStateName, 0.99f);
            animator.speed = gettingUp ? getUpClipSpeed : knockdownClipSpeed;
            measuredSpeed = speed;
            appliedAnimSpeed = animator.speed;
            return; // mientras estás en el suelo no se toca el Blend Tree
        }

        // Esquivar: al empezar el roll se reproduce su animación, ajustada a la duración real.
        bool rolling = player != null && player.IsRolling;
        if (rolling && !wasRolling)
        {
            // PlayerController decide qué roll toca (caminando, corriendo o barrido).
            bool isRun = player.CurrentRollIsRun;
            bool isAlt = player.CurrentRollIsAlt;
            string stateName = isAlt ? rollRunAltStateName : (isRun ? rollRunStateName : rollWalkStateName);

            // Con la cámara fijada: hacia atrás o de lado.
            PlayerController.LockedRollKind lockedKind = player.CurrentLockedRoll;
            if (lockedKind == PlayerController.LockedRollKind.Back) stateName = rollBackStateName;
            else if (lockedKind == PlayerController.LockedRollKind.Left) stateName = rollSideLeftStateName;
            else if (lockedKind == PlayerController.LockedRollKind.Right) stateName = rollSideRightStateName;
            currentRollIsLocked = lockedKind != PlayerController.LockedRollKind.None;

            currentRollStateName = stateName;
            if (debugLogs)
                Debug.Log("[PlayerAnimator] Roll: tipo=" + lockedKind + " bloqueado=" + player.IsLockedOn +
                          " estado='" + stateName + "' existe=" + animator.HasState(0, Animator.StringToHash(stateName)));
            animator.CrossFadeInFixedTime(stateName, rollCrossFade, 0, 0f);
            rollClipSpeed = MeasureRollClipSpeed();

            currentYawCompensation = currentRollIsLocked ? 0f
                : (isAlt ? rollRunAltYawCompensation
                    : (isRun ? rollRunYawCompensation : rollWalkYawCompensation));
            rollStartTime = Time.time;
            rollTotalTime = Mathf.Max(0.05f, player.CurrentRollDuration + player.RollRecoveryTime);
        }
        wasRolling = rolling;

        // La animación cubre el roll y el tiempo de levantarse.
        bool inRollPhase = rolling || (player != null && player.IsRollRecovering);

        // Giro de compensación: va de 0 a los grados indicados a lo largo del roll y la recuperación.
        if (modelTransform != null)
        {
            if (inRollPhase && Mathf.Abs(currentYawCompensation) > 0.01f)
            {
                float progress = Mathf.Clamp01((Time.time - rollStartTime) / rollTotalTime);
                appliedYaw = currentYawCompensation * progress;
                modelTransform.localRotation = modelBaseRotation * Quaternion.Euler(0f, appliedYaw, 0f);
                yawApplied = true;
                yawReleaseStart = -1f;
            }
            else if (yawApplied)
            {
                // Al terminar el roll la compensación se suelta poco a poco (sin saltos),
                // mientras el Animator mezcla de vuelta a caminar.
                if (yawReleaseStart < 0f) yawReleaseStart = Time.time;
                float t = Mathf.Clamp01((Time.time - yawReleaseStart) / Mathf.Max(rollYawReleaseTime, 0.01f));
                modelTransform.localRotation = modelBaseRotation * Quaternion.Euler(0f, appliedYaw * (1f - t), 0f);

                if (t >= 1f)
                {
                    modelTransform.localRotation = modelBaseRotation;
                    yawApplied = false;
                    yawReleaseStart = -1f;
                }
            }
        }

        if (inRollPhase)
        {
            stance = StanceState.None; // al terminar se vuelve a elegir la pose de pelea
            exitPending = true;
            exitFade = (currentRollIsLocked || currentRollStateName == rollWalkStateName)
                ? rollWalkExitCrossFade : exitCrossFade;
            HoldState(currentRollStateName, Mathf.Clamp01((Time.time - rollStartTime) / rollTotalTime));
            animator.speed = rollClipSpeed;
            measuredSpeed = speed;
            appliedAnimSpeed = animator.speed;
            return; // mientras ruedas no se toca el Blend Tree
        }

        // Ataque: la animación se ajusta a la duración total del golpe (preparación + daño + recuperación).
        bool attacking = combat != null && combat.IsAttacking;
        if (attacking && combat.AttackSerial != lastAttackSerial)
        {
            lastAttackSerial = combat.AttackSerial;

            string attackState = attackLightStateName;
            if (combat.CurrentAttackIsHeavy && animator.HasState(0, Animator.StringToHash(attackHeavyStateName)))
                attackState = attackHeavyStateName;

            currentAttackStateName = attackState;
            if (debugLogs)
                Debug.Log("[PlayerAnimator] Ataque: pesado=" + combat.CurrentAttackIsHeavy + " estado='" + attackState +
                          "' existe=" + animator.HasState(0, Animator.StringToHash(attackState)));
            animator.CrossFadeInFixedTime(attackState, attackCrossFade, 0, 0f);
            attackClipSpeed = MeasureClipSpeed(combat.CurrentAttackDuration);
        }

        if (attacking)
        {
            stance = StanceState.None;
            exitPending = true;
            exitFade = attackExitCrossFade;
            HoldState(currentAttackStateName, 0.99f);
            animator.speed = attackClipSpeed;
            measuredSpeed = speed;
            appliedAnimSpeed = animator.speed;
            return;
        }

        if (hasSpeedParameter)
            animator.SetFloat(speedHash, speed, dampTime, dt);

        // Con la cámara fijada: pose de pelea, y la animación depende de hacia dónde caminas.
        UpdateStance(delta, dt, speed);

        bool moving = speed > 0.1f;

        if (freezeWhenStopped)
        {
            animator.speed = moving ? PlaybackSpeed(speed) : 0f;
        }
        else
        {
            animator.speed = moving ? PlaybackSpeed(speed) : 1f;
        }

        // Caminar con la cámara fijada: velocidad propia de las poses de guardia.
        if (moving && stance != StanceState.None && stance != StanceState.Idle)
            animator.speed = stanceMoveAnimSpeed;

        // Pose de guardia quieto: solo se congela si activas la casilla (útil si el clip no es un idle real).
        if (freezeStanceIdle && stance == StanceState.Idle && !animator.IsInTransition(0))
            animator.speed = 0f;

        measuredSpeed = speed;
        appliedAnimSpeed = animator.speed;
    }

    // Si una flecha del Animator sacó al personaje de un estado especial (roll, levantarse) antes de tiempo,
    // se le devuelve a ese estado para que el código decida a dónde ir al terminar.
    private void HoldState(string stateName, float normalizedTime)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash)) return;

        AnimatorStateInfo cur = animator.GetCurrentAnimatorStateInfo(0);
        if (cur.shortNameHash == hash) return;

        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == hash) return;

        animator.Play(hash, 0, normalizedTime);
    }

    // ¿El Animator está (o va a estar) en alguno de los estados de pose de pelea?
    // Durante una mezcla solo importa el estado de destino.
    private bool IsInStanceState()
    {
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        return info.IsName(stanceIdleState) || info.IsName(stanceForwardState)
            || info.IsName(stanceBackState) || info.IsName(stanceLeftState)
            || info.IsName(stanceRightState);
    }

    // Avisa en la Console si algún nombre de estado no existe en el Animator.
    private void ValidateStates()
    {
        string[] names =
        {
            rollWalkStateName, rollRunStateName, rollRunAltStateName, knockdownStateName, getUpStateName,
            rollBackStateName, rollSideLeftStateName, rollSideRightStateName,
            attackLightStateName,
            stanceIdleState, stanceForwardState, stanceBackState, stanceLeftState, stanceRightState,
            locomotionStateName
        };

        foreach (string stateName in names)
        {
            if (string.IsNullOrEmpty(stateName)) continue;
            if (!animator.HasState(0, Animator.StringToHash(stateName)))
            {
                Debug.LogWarning("PlayerAnimator: el Animator no tiene un estado llamado '" + stateName +
                                 "' en Base Layer. Revisa el nombre en la ventana Animator o en este componente.", this);
            }
        }
    }

    // Pose de pelea con la cámara fijada. Elige el estado según la dirección de movimiento
    // respecto a hacia dónde mira el personaje. Al soltar la cámara, vuelve a Locomotion.
    private void UpdateStance(Vector3 delta, float dt, float speed)
    {
        bool locked = player != null && player.IsLockedOn;

        // Si justo terminó un roll o un derribo, el código decide a dónde volver
        // (las flechas de salida del Animator ya no hacen falta).
        bool leftSpecialState = exitPending;
        exitPending = false;
        float fade = leftSpecialState ? exitFade : stanceCrossFade;

        if (!locked)
        {
            // Sin cámara fijada: se regresa a Locomotion si venimos de un roll/derribo o de una pose de pelea.
            if (stance != StanceState.None || leftSpecialState || IsInStanceState())
            {
                stance = StanceState.None;

                // Se vuelve a Locomotion por nombre; si ese nombre no existe, al estado inicial del Animator.
                int locomotionHash = Animator.StringToHash(locomotionStateName);
                if (!animator.HasState(0, locomotionHash)) locomotionHash = defaultStateHash;
                animator.CrossFadeInFixedTime(locomotionHash, fade, 0, 0f);
            }
            return;
        }

        // Si el Animator salió de la pose por su cuenta (por ejemplo al terminar un roll,
        // cuyo estado regresa a Locomotion), se vuelve a entrar a la pose de pelea.
        if (stance != StanceState.None && !IsInStanceState())
            stance = StanceState.None;

        // Justo al salir de un roll la velocidad medida aún es casi 0 aunque tengas la tecla presionada.
        // En ese momento se usa la dirección de las teclas para elegir la pose correcta desde el primer fotograma.
        Vector3 moveDir = delta / dt;
        bool useInput = false;
        if (leftSpecialState && speed < 0.3f)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Camera cam = Camera.main;
            if ((Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f) && cam != null)
            {
                Vector3 camF = cam.transform.forward; camF.y = 0f; camF.Normalize();
                Vector3 camR = cam.transform.right; camR.y = 0f; camR.Normalize();
                moveDir = (camF * v + camR * h).normalized;
                useInput = true;
            }
        }

        StanceState wanted;
        if (speed < 0.3f && !useInput)
        {
            wanted = StanceState.Idle;
        }
        else
        {
            // La dirección se mide respecto a la cámara (que mira al enemigo) y no al cuerpo,
            // porque tras un roll el cuerpo tarda en volver a mirar al enemigo.
            Vector3 local;
            Camera refCam = Camera.main;
            if (refCam != null)
            {
                Vector3 rf = refCam.transform.forward; rf.y = 0f; rf.Normalize();
                Vector3 rr = refCam.transform.right; rr.y = 0f; rr.Normalize();
                local = new Vector3(Vector3.Dot(moveDir, rr), 0f, Vector3.Dot(moveDir, rf));
            }
            else
            {
                local = transform.InverseTransformDirection(moveDir);
            }
            if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
                wanted = local.z >= 0f ? StanceState.Forward : StanceState.Back;
            else
                wanted = local.x >= 0f ? StanceState.Right : StanceState.Left;
        }

        if (wanted == stance) return;
        if (stance != StanceState.None && Time.time - stanceChangedAt < stanceMinHold) return;

        stance = wanted;
        stanceChangedAt = Time.time;

        string stateName;
        switch (wanted)
        {
            case StanceState.Forward: stateName = stanceForwardState; break;
            case StanceState.Back: stateName = stanceBackState; break;
            case StanceState.Left: stateName = stanceLeftState; break;
            case StanceState.Right: stateName = stanceRightState; break;
            default: stateName = stanceIdleState; break;
        }

        animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
    }

    // La duración del roll la decide PlayerController (Roll Walk Duration / Roll Run Duration).
    // Se incluye el tiempo de recuperación: la animación se estira hasta que terminas de levantarte.
    private float MeasureRollClipSpeed()
    {
        float rollDuration = player != null ? player.CurrentRollDuration + player.RollRecoveryTime : 0.5f;
        return MeasureClipSpeed(rollDuration);
    }

    // Acelera o frena la animación del estado al que el Animator está pasando
    // para que dure exactamente targetDuration segundos.
    private float MeasureClipSpeed(float targetDuration)
    {
        if (targetDuration <= 0.01f) return 1f;

        animator.Update(0f); // aplica el cambio de estado ahora mismo para poder leerlo
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        if (info.length <= 0.01f || float.IsInfinity(info.length)) return 1f;
        return Mathf.Clamp(info.length / targetDuration, 0.5f, 3f);
    }

    private float PlaybackSpeed(float speed)
    {
        if (scalePlaybackWithSpeed)
            return Mathf.Clamp(speed / Mathf.Max(referenceSpeed, 0.01f), 0.6f, maxPlaybackSpeed);

        // Entre la velocidad de caminar y la de correr, la animación pasa poco a poco de una a otra.
        float t = Mathf.InverseLerp(walkSpeedReference, runSpeedReference, speed);
        return Mathf.Lerp(walkAnimSpeed, runAnimSpeed, t);
    }
}