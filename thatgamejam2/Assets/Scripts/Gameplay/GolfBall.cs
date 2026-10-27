using UnityEngine;
using UnityEngine.InputSystem;

public class GolfBall : MonoBehaviour
{
    enum State { Aiming, Power, Moving }

    [Header("Aim")]
    [SerializeField] float minAngle = 0f;
    [SerializeField] float maxAngle = 80f;
    [SerializeField] float angleSpeed = 90f;
    [SerializeField] float arrowLength = 1.5f;

    [Header("Power")]
    [SerializeField] float powerSpeed = 1.2f;
    [SerializeField] float maxLaunchSpeed = 20f;

    [Header("Stop")]
    [SerializeField] float stopSepeed = 0.15f;
    [SerializeField] float stopTime = 0.6f;

    Rigidbody2D rb;
    LineRenderer arrow;

    State state = State.Aiming;

    float aimTime;
    float powerTime;
    float angle;
    float power;
    float stillTimer;
    Vector2 startPos;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        arrow = gameObject.AddComponent<LineRenderer>();
        arrow.positionCount = 2;
        arrow.startWidth = arrow.endWidth = 0.08f;
        arrow.material = new Material(Shader.Find("Sprites/Default"));
        arrow.startColor = arrow.endColor = Color.yellow;

        startPos = rb.position;
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        bool pressed = Keyboard.current.spaceKey.wasPressedThisFrame;

        if(Keyboard.current.rKey.wasPressedThisFrame)
        {
            rb.position = startPos;
            BeginAim();
            return;
        }

        if (state == State.Aiming)
        {
            aimTime += Time.deltaTime * angleSpeed;
            angle = Mathf.Lerp(minAngle, maxAngle, Mathf.PingPong(aimTime / (maxAngle - minAngle), 1f));
            DrawArrow(arrowLength);

            if (pressed) state = State.Power;
        }
        else if (state == State.Power)
        {
            powerTime += Time.deltaTime * powerSpeed;
            power = Mathf.PingPong(powerTime, 1f);
            DrawArrow(arrowLength * Mathf.Max(0.2f, power));

            if (pressed) Launch();
        }
        else if(state == State.Moving)
        {
            if(rb.linearVelocity.magnitude < stopSepeed)
            {
                stillTimer += Time.deltaTime;
            }
            else
            {
                stillTimer = 0;
            }
            if(stillTimer >= stopTime)
            {
                BeginAim();
            }
        }
    }

    void DrawArrow(float length)
    {
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        Vector2 pos = transform.position;
        arrow.SetPosition(0, pos);
        arrow.SetPosition(1, pos + dir * length);
    }


    void BeginAim()
    {
        state = State.Aiming;
        aimTime = 0f;
        powerTime = 0f;
        power = 0f;
        stillTimer = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        arrow.enabled = true;
    }

    void Launch()
    {
        state = State.Moving;
        arrow.enabled = false;
        rb.gravityScale = 1f;

        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        rb.linearVelocity = dir * (power * maxLaunchSpeed);
    }

    void OnGUI()
    {
        if (state == State.Moving) return;

        Rect box = new Rect(Screen.width / 2f - 100f, Screen.height - 50f, 200f, 20f);
        GUI.Box(box, GUIContent.none);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width * power, box.height), Texture2D.whiteTexture);
    }
}