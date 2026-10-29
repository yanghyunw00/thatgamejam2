using System;
using UnityEngine;

/// <summary>
/// 컵(Hole) 판정. RegionGenerator가 Hole 마커 위치에 자동으로 만들어줌. 직접 붙일 일 없음.
/// 공이 컵 안에 있고 속도가 maxSpeed 이하로 settleTime 동안 유지되면 Holed 신호.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class HoleTrigger : MonoBehaviour
{
    public Rigidbody2D target;          // 비어 있으면 처음 들어온 Rigidbody2D를 공으로 간주
    public float maxSpeed = 1.5f;     // 이 속도 이하일 때만 홀인 (아주 크게 하면 닿자마자 홀인)
    public float settleTime = 0.15f;    // 그 상태가 이만큼 유지돼야 홀인

    public event Action Holed;

    bool _inside, _fired;
    float _timer;

    void OnTriggerEnter2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        if (target == null) target = rb;
        if (rb == target) _inside = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.attachedRigidbody == target) { _inside = false; _timer = 0f; }
    }

    void Update()
    {
        if (_fired || !_inside || target == null) return;

        if (target.linearVelocity.magnitude <= maxSpeed)
        {
            _timer += Time.deltaTime;
            if (_timer >= settleTime)
            {
                _fired = true;
                Holed?.Invoke();
            }
        }
        else _timer = 0f;
    }
}