using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlatformCameraFollow : MonoBehaviour
{
    public Transform target;
    public Rigidbody2D targetRigidbody;

    [Header("Follow")]
    public float smoothTimeX = 0.16f;
    public float smoothTimeY = 0.2f;
    public Vector2 deadZone = new Vector2(0.45f, 0.3f);
    public Vector2 baseOffset = new Vector2(0f, 0.45f);

    [Header("Look Ahead")]
    public float horizontalLookAheadDistance = 1.5f;
    public float horizontalLookAheadSmooth = 7f;
    public float minLookAheadSpeed = 0.4f;

    [Header("Fall Framing")]
    public float fallLookDownDistance = 1.2f;
    public float fallLookDownSmooth = 4f;
    public float minFallSpeed = 2.2f;

    [Header("Clamp")]
    public bool clampToWorld = false;
    public Vector2 minBounds = new Vector2(-10f, -5f);
    public Vector2 maxBounds = new Vector2(10f, 5f);

    private Camera cachedCamera;
    private float velocityX;
    private float velocityY;
    private float currentLookAheadX;
    private float currentLookDownY;

    void Awake()
    {
        cachedCamera = GetComponent<Camera>();

        if (target != null && targetRigidbody == null)
            targetRigidbody = target.GetComponent<Rigidbody2D>();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        if (targetRigidbody == null)
            targetRigidbody = target.GetComponent<Rigidbody2D>();

        UpdateLookAhead();
        UpdateLookDown();

        Vector3 currentPosition = transform.position;
        Vector2 desiredCenter = (Vector2)target.position + baseOffset + new Vector2(currentLookAheadX, currentLookDownY);
        Vector2 cameraCenter = new Vector2(currentPosition.x, currentPosition.y);
        Vector2 delta = desiredCenter - cameraCenter;

        if (Mathf.Abs(delta.x) <= deadZone.x)
            desiredCenter.x = cameraCenter.x;

        if (Mathf.Abs(delta.y) <= deadZone.y)
            desiredCenter.y = cameraCenter.y;

        float nextX = Mathf.SmoothDamp(currentPosition.x, desiredCenter.x, ref velocityX, smoothTimeX);
        float nextY = Mathf.SmoothDamp(currentPosition.y, desiredCenter.y, ref velocityY, smoothTimeY);
        Vector3 nextPosition = new Vector3(nextX, nextY, currentPosition.z);

        if (clampToWorld && cachedCamera != null && cachedCamera.orthographic)
            nextPosition = ClampToBounds(nextPosition);

        transform.position = nextPosition;
    }

    void UpdateLookAhead()
    {
        float targetLookAhead = 0f;
        if (targetRigidbody != null && Mathf.Abs(targetRigidbody.velocity.x) > minLookAheadSpeed)
            targetLookAhead = Mathf.Sign(targetRigidbody.velocity.x) * horizontalLookAheadDistance;

        currentLookAheadX = Mathf.Lerp(currentLookAheadX, targetLookAhead, 1f - Mathf.Exp(-horizontalLookAheadSmooth * Time.deltaTime));
    }

    void UpdateLookDown()
    {
        float targetLookDown = 0f;
        if (targetRigidbody != null && targetRigidbody.velocity.y < -minFallSpeed)
            targetLookDown = -fallLookDownDistance;

        currentLookDownY = Mathf.Lerp(currentLookDownY, targetLookDown, 1f - Mathf.Exp(-fallLookDownSmooth * Time.deltaTime));
    }

    Vector3 ClampToBounds(Vector3 position)
    {
        float halfHeight = cachedCamera.orthographicSize;
        float halfWidth = halfHeight * cachedCamera.aspect;

        float clampedX = Mathf.Clamp(position.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
        float clampedY = Mathf.Clamp(position.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);
        return new Vector3(clampedX, clampedY, position.z);
    }
}
