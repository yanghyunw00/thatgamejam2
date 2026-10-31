using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GolfBall : MonoBehaviour
{
    enum State { Aiming, Moving }

    [Header("Hit")]
    [SerializeField] float pointerRadius = 0.1f;
    [SerializeField] float minHitSpeed = 3f;
    [SerializeField] float fullPowerMouseSpeed = 30f;
    [SerializeField] float powerCurve = 1.6f;
    [SerializeField] float minLaunchSpeed = 4f;
    [SerializeField] float maxLaunchSpeed = 40f;
    [SerializeField] float velocitySmoothing = 30f;

    [Header("Gravity")]
    [SerializeField] float flightGravityScale = 3f;

    [Header("Damping")]
    [SerializeField] float linearDamping = 0.3f;
    [SerializeField] float angularDamping = 3f;
    [SerializeField] float slowSpeed = 2f;
    [SerializeField] float slowLinearDamping = 4f;

    [Header("Stop")]
    [SerializeField] float settleSpeed = 0.8f;
    [SerializeField] float settleTime = 0.25f;

    public int Strokes => strokes;
    public bool CanHit => state == State.Aiming;
    public event Action<float> ShotLaunched;
    public event Action<float> Impacted;

    Rigidbody2D rb;
    float ballRadius = 0.25f;

    State state = State.Aiming;

    float stillTimer;
    int strokes;
    Vector2 startPos;

    Vector2 prevMouseWorld;
    Vector2 mouseVelocity;
    bool hasPrevMouse;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var circle = GetComponent<CircleCollider2D>();
        if (circle != null)
            ballRadius = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        startPos = rb.position;
    }

    void Update()
    {
        if (GameCameraController.IsIntroActive)
        {
            hasPrevMouse = false;
            mouseVelocity = Vector2.zero;
            return;
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            rb.position = startPos;
            strokes = 0;
            BeginAim();
            return;
        }

        if (state == State.Aiming)
        {
            TryHit();
        }
        else if (state == State.Moving)
        {
            rb.linearDamping = rb.linearVelocity.magnitude < slowSpeed ? slowLinearDamping : linearDamping;

            bool grounded = rb.IsTouchingLayers();
            if (grounded && rb.linearVelocity.magnitude < settleSpeed)
            {
                stillTimer += Time.deltaTime;
            }
            else
            {
                stillTimer = 0;
            }
            if (stillTimer >= settleTime)
            {
                BeginAim();
            }
        }
    }

    void TryHit()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());

        if (!hasPrevMouse)
        {
            prevMouseWorld = mouseWorld;
            mouseVelocity = Vector2.zero;
            hasPrevMouse = true;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector2 rawVelocity = (mouseWorld - prevMouseWorld) / dt;
        mouseVelocity = Vector2.Lerp(mouseVelocity, rawVelocity, 1f - Mathf.Exp(-velocitySmoothing * dt));

        float dist = DistanceToSegment(rb.position, prevMouseWorld, mouseWorld);
        bool touching = dist <= ballRadius + pointerRadius;
        float speed = mouseVelocity.magnitude;

        if (touching && speed >= minHitSpeed)
        {
            float t = Mathf.Pow(Mathf.Clamp01(speed / fullPowerMouseSpeed), powerCurve);
            Launch(mouseVelocity.normalized, Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, t));
        }

        prevMouseWorld = mouseWorld;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }

    void BeginAim()
    {
        state = State.Aiming;
        stillTimer = 0f;
        hasPrevMouse = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
    }

    void Launch(Vector2 dir, float speed)
    {
        state = State.Moving;
        strokes++;
        rb.gravityScale = flightGravityScale;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.linearVelocity = dir * speed;
        ShotLaunched?.Invoke(speed);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Impacted?.Invoke(collision.relativeVelocity.magnitude);
    }
}
