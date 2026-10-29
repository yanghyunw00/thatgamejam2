using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector2 offset = new Vector2(2f, 1f);
    [SerializeField] float smoothTime = 0.2f;

    Vector3 veloicity;

    void LateUpdate()
    {
        if(target == null)
        {
            return;
        }

        Vector3 goal = new Vector3(target.position.x + offset.x,
                                    target.position.y + offset.y,
                                    transform.position.z);

        transform.position = Vector3.SmoothDamp(transform.position, goal, ref veloicity, smoothTime);
    }
}
