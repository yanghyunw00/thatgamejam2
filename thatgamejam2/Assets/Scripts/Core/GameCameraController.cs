using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class GameCameraController : MonoBehaviour
{
    public static bool IsIntroActive { get; private set; }

    [Header("Targets")]
    [SerializeField] Transform playerTarget;
    [SerializeField] Transform startTarget;
    [SerializeField] Transform goalTarget;

    [Header("Follow")]
    [SerializeField] Vector2 followOffset = new Vector2(0f, 0.5f);
    [SerializeField, Min(0.01f)] float followSmoothTime = 0.15f;
    [SerializeField, Min(0f)] float maxLookAheadDistance = 3f;
    [SerializeField, Min(0.01f)] float lookAheadSmoothTime = 0.3f;
    [SerializeField] Vector2 introOffset;

    [Header("Intro")]
    [SerializeField] bool playIntro = true;
    [SerializeField, Min(0f)] float startHoldDuration = 1f;
    [SerializeField, Min(0f)] float panToGoalDuration = 5f;
    [SerializeField, Min(0f)] float goalHoldDuration = 1f;
    [SerializeField, Min(0f)] float panToStartDuration = 3f;
    [SerializeField, Min(0f)] float introVerticalPadding = 0.5f;

    [Header("Dynamic Zoom")]
    [SerializeField, Min(0.01f)] float playSize = 6f;
    [SerializeField, Min(0.01f)] float maxSpeedZoomSize = 15f;
    [SerializeField, Min(0.01f)] float speedForMaxZoom = 40f;
    [SerializeField, Min(0.01f)] float zoomSmoothTime = 0.65f;

    [Header("Camera Effects")]
    [SerializeField, Min(0f)] float shotZoomPunch = 0.7f;
    [SerializeField, Min(0.01f)] float shotPunchDuration = 0.25f;
    [SerializeField, Min(0f)] float minimumImpactSpeed = 3f;
    [SerializeField, Min(0.01f)] float fullImpactShakeSpeed = 20f;
    [SerializeField, Min(0f)] float maxShakeDistance = 0.15f;
    [SerializeField, Range(0f, 1f)] float shakeDistanceScale = 0.33f;
    [SerializeField, Min(0.01f)] float shakeDuration = 0.2f;
    [SerializeField, Min(0.01f)] float shakeFrequency = 25f;

    Camera controlledCamera;
    Rigidbody2D playerBody;
    Vector3 positionVelocity;
    Vector2 lookAheadOffset;
    Vector2 lookAheadVelocity;
    float zoomVelocity;
    bool hasGoalFocus;
    bool hasStartFocus;
    Vector3 goalFocus;
    Vector3 startFocus;
    Vector2 mapBoundsMin;
    Vector2 mapBoundsMax;
    bool hasMapBounds;
    float introSize;
    float shotPunchRemaining;
    float shakeRemaining;
    float shakeStrength;
    float shakePhase;
    Coroutine introRoutine;

    void OnEnable()
    {
        SubscribeToBall();
    }

    void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        if (!controlledCamera.orthographic)
        {
            Debug.LogError($"{nameof(GameCameraController)} requires an orthographic Camera.", this);
            enabled = false;
            return;
        }

        ResolveTargets();
        SubscribeToBall();
        bool hasBounds = TryGetMapBounds();
        if (hasBounds)
            introSize = Mathf.Max(playSize, (mapBoundsMax.y - mapBoundsMin.y) * 0.5f + introVerticalPadding);

        if (playIntro && hasGoalFocus && hasStartFocus && hasBounds)
        {
            transform.position = ClampToMapBounds(CameraPosition(startFocus, followOffset), introSize);
            controlledCamera.orthographicSize = introSize;
        }
        else
        {
            Vector3 initialFocus = hasStartFocus ? startFocus : playerTarget != null ? playerTarget.position : transform.position;
            transform.position = CameraPosition(initialFocus, followOffset);
            controlledCamera.orthographicSize = playSize;
        }
    }

    void Start()
    {
        if (!playIntro)
            return;

        if (!hasGoalFocus || !hasStartFocus || !hasMapBounds)
        {
            Debug.LogWarning($"{nameof(GameCameraController)} could not find map bounds, Goal, or Start/Tee position; intro is skipped.", this);
            return;
        }

        IsIntroActive = true;
        introRoutine = StartCoroutine(PlayIntroSequence());
    }

    void LateUpdate()
    {
        if (IsIntroActive)
            return;

        if (playerTarget == null)
            return;

        Vector2 velocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
        float speed = velocity.magnitude;
        float speedFactor = Mathf.Clamp01(speed / speedForMaxZoom);
        Vector2 desiredLookAhead = speed > 0.01f
            ? velocity.normalized * (maxLookAheadDistance * speedFactor)
            : Vector2.zero;
        lookAheadOffset = Vector2.SmoothDamp(
            lookAheadOffset,
            desiredLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime);

        Vector3 targetPosition = playerTarget.position + (Vector3)(followOffset + lookAheadOffset);
        targetPosition.z = -10f;
        Vector3 followedPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref positionVelocity,
            followSmoothTime);

        float zoomAmount = Mathf.Clamp01(speed / speedForMaxZoom);
        float targetSize = Mathf.Lerp(playSize, maxSpeedZoomSize, zoomAmount);
        targetSize = Mathf.Max(playSize * 0.8f, targetSize - GetShotPunch());
        controlledCamera.orthographicSize = Mathf.SmoothDamp(
            controlledCamera.orthographicSize,
            targetSize,
            ref zoomVelocity,
            zoomSmoothTime);

        followedPosition += (Vector3)GetShakeOffset();
        followedPosition = ClampToMapBounds(followedPosition, controlledCamera.orthographicSize);
        transform.position = followedPosition;
    }

    void OnDisable()
    {
        UnsubscribeFromBall();

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = null;
        IsIntroActive = false;
    }

    void SubscribeToBall()
    {
        if (playerTarget == null)
            return;

        GolfBall ball = playerTarget.GetComponent<GolfBall>();
        if (ball == null)
            return;

        ball.ShotLaunched -= OnShotLaunched;
        ball.Impacted -= OnBallImpacted;
        ball.ShotLaunched += OnShotLaunched;
        ball.Impacted += OnBallImpacted;
    }

    void UnsubscribeFromBall()
    {
        if (playerTarget == null)
            return;

        GolfBall ball = playerTarget.GetComponent<GolfBall>();
        if (ball == null)
            return;

        ball.ShotLaunched -= OnShotLaunched;
        ball.Impacted -= OnBallImpacted;
    }

    void OnShotLaunched(float launchSpeed)
    {
        shotPunchRemaining = shotPunchDuration;
    }

    void OnBallImpacted(float impactSpeed)
    {
        float impactRange = Mathf.Max(0.01f, fullImpactShakeSpeed - minimumImpactSpeed);
        shakeStrength = Mathf.Clamp01((impactSpeed - minimumImpactSpeed) / impactRange);
        if (shakeStrength <= 0f)
            return;

        shakeRemaining = shakeDuration;
        shakePhase = Random.Range(0f, 100f);
    }

    float GetShotPunch()
    {
        if (shotPunchRemaining <= 0f)
            return 0f;

        float remaining = shotPunchRemaining / shotPunchDuration;
        shotPunchRemaining = Mathf.Max(0f, shotPunchRemaining - Time.deltaTime);
        return shotZoomPunch * Mathf.SmoothStep(0f, 1f, remaining);
    }

    Vector2 GetShakeOffset()
    {
        if (shakeRemaining <= 0f)
            return Vector2.zero;

        float remaining = shakeRemaining / shakeDuration;
        shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
        float envelope = Mathf.SmoothStep(0f, 1f, remaining) * shakeStrength * maxShakeDistance * shakeDistanceScale;
        float time = Time.time * shakeFrequency + shakePhase;
        return new Vector2(Mathf.Sin(time), Mathf.Sin(time * 1.37f + 1.2f)) * envelope;
    }

    Vector3 ClampToMapBounds(Vector3 position, float orthographicSize)
    {
        if (!hasMapBounds)
            return position;

        float halfHeight = orthographicSize;
        float halfWidth = halfHeight * controlledCamera.aspect;
        position.x = ClampCameraAxis(position.x, mapBoundsMin.x, mapBoundsMax.x, halfWidth);
        position.y = ClampCameraAxis(position.y, mapBoundsMin.y, mapBoundsMax.y, halfHeight);
        return position;
    }

    static float ClampCameraAxis(float position, float mapMin, float mapMax, float viewportHalfSize)
    {
        float min = mapMin + viewportHalfSize;
        float max = mapMax - viewportHalfSize;
        return min > max ? (mapMin + mapMax) * 0.5f : Mathf.Clamp(position, min, max);
    }

    IEnumerator PlayIntroSequence()
    {
        IsIntroActive = true;
        positionVelocity = Vector3.zero;

        transform.position = ClampToMapBounds(CameraPosition(startFocus, followOffset), introSize);
        controlledCamera.orthographicSize = introSize;
        if (startHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(startHoldDuration);

        Vector3 goalPosition = CameraPosition(goalFocus, introOffset);
        yield return AnimateCamera(
            transform.position,
            goalPosition,
            controlledCamera.orthographicSize,
            introSize,
            panToGoalDuration);

        if (goalHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(goalHoldDuration);

        Vector3 startPosition = CameraPosition(startFocus, followOffset);
        yield return AnimateCamera(
            transform.position,
            startPosition,
            controlledCamera.orthographicSize,
            introSize,
            panToStartDuration);

        positionVelocity = Vector3.zero;
        IsIntroActive = false;
        introRoutine = null;
    }

    IEnumerator AnimateCamera(Vector3 fromPosition, Vector3 toPosition, float fromSize, float toSize, float duration)
    {
        if (duration <= 0f)
        {
            controlledCamera.orthographicSize = toSize;
            transform.position = ClampToMapBounds(toPosition, toSize);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / duration));
            controlledCamera.orthographicSize = Mathf.Lerp(fromSize, toSize, t);
            transform.position = ClampToMapBounds(Vector3.Lerp(fromPosition, toPosition, t), controlledCamera.orthographicSize);
            yield return null;
        }

        controlledCamera.orthographicSize = toSize;
        transform.position = ClampToMapBounds(toPosition, toSize);
    }

    static float EaseInOut(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    void ResolveTargets()
    {
        if (playerTarget == null)
        {
            GolfBall ball = FindFirstObjectByType<GolfBall>();
            if (ball != null)
                playerTarget = ball.transform;
        }

        if (playerTarget != null)
            playerBody = playerTarget.GetComponent<Rigidbody2D>();

        if (goalTarget != null)
        {
            goalFocus = goalTarget.position;
            hasGoalFocus = true;
        }
        else
        {
            Transform namedHole = FindNamedTarget("Hole");
            if (namedHole != null)
            {
                goalTarget = namedHole;
                goalFocus = namedHole.position;
                hasGoalFocus = true;
            }
            else
            {
                hasGoalFocus = TryFindSectionMarker(SectionTier.Goal, SectionInfo.HoleMarker, out goalFocus);
            }
        }

        if (startTarget != null)
        {
            startFocus = startTarget.position;
            hasStartFocus = true;
        }
        else
        {
            Transform namedTee = FindNamedTarget("Tee");
            if (namedTee != null)
            {
                startTarget = namedTee;
                startFocus = namedTee.position;
                hasStartFocus = true;
            }
            else if (!TryFindSectionMarker(SectionTier.Start, SectionInfo.TeeMarker, out startFocus))
            {
                if (playerTarget != null)
                {
                    startFocus = playerTarget.position;
                    hasStartFocus = true;
                }
            }
            else
            {
                hasStartFocus = true;
            }
        }
    }

    static Transform FindNamedTarget(string objectName)
    {
        GameObject targetObject = GameObject.Find(objectName);
        return targetObject != null ? targetObject.transform : null;
    }

    static bool TryFindSectionMarker(SectionTier tier, string markerName, out Vector3 position)
    {
        foreach (SectionInfo section in FindObjectsByType<SectionInfo>(FindObjectsSortMode.None))
        {
            if (section.tier != tier || section.CountMarker(markerName, out Vector3Int cell) == 0)
                continue;

            position = section.Markers.GetCellCenterWorld(cell);
            return true;
        }

        position = default;
        return false;
    }

    bool TryGetMapBounds()
    {
        SectionInfo[] sections = FindObjectsByType<SectionInfo>(FindObjectsSortMode.None);
        if (sections.Length == 0)
        {
            mapBoundsMin = Vector2.Min(startFocus, goalFocus);
            mapBoundsMax = Vector2.Max(startFocus, goalFocus);
            hasMapBounds = true;
            return true;
        }

        Vector3 firstCorner = sections[0].transform.TransformPoint(Vector3.zero);
        Vector2 min = firstCorner;
        Vector2 max = firstCorner;

        foreach (SectionInfo section in sections)
        {
            Vector3[] corners =
            {
                section.transform.TransformPoint(Vector3.zero),
                section.transform.TransformPoint(new Vector3(section.width, 0f)),
                section.transform.TransformPoint(new Vector3(0f, SectionInfo.Height)),
                section.transform.TransformPoint(new Vector3(section.width, SectionInfo.Height))
            };

            foreach (Vector3 corner in corners)
            {
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
        }

        mapBoundsMin = min;
        mapBoundsMax = max;
        hasMapBounds = true;
        return true;
    }

    static Vector3 CameraPosition(Vector3 focus, Vector2 offset)
    {
        return new Vector3(focus.x + offset.x, focus.y + offset.y, -10f);
    }

    void OnValidate()
    {
        playSize = Mathf.Max(0.01f, playSize);
        maxSpeedZoomSize = Mathf.Max(playSize, maxSpeedZoomSize);
        speedForMaxZoom = Mathf.Max(0.01f, speedForMaxZoom);
        followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
        lookAheadSmoothTime = Mathf.Max(0.01f, lookAheadSmoothTime);
        zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
        shotPunchDuration = Mathf.Max(0.01f, shotPunchDuration);
        fullImpactShakeSpeed = Mathf.Max(minimumImpactSpeed + 0.01f, fullImpactShakeSpeed);
        shakeDuration = Mathf.Max(0.01f, shakeDuration);
        shakeFrequency = Mathf.Max(0.01f, shakeFrequency);
        startHoldDuration = Mathf.Max(0f, startHoldDuration);
        panToGoalDuration = Mathf.Max(0f, panToGoalDuration);
        goalHoldDuration = Mathf.Max(0f, goalHoldDuration);
        panToStartDuration = Mathf.Max(0f, panToStartDuration);
    }
}
