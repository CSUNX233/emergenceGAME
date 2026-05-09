using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FallingCrushHazard : MonoBehaviour
{
    public float minimumDownwardSpeed = 3.85f;
    public float damage = 999f;
    public bool requireHitFromAbove = true;
    public float topHitTolerance = 0.08f;

    private Rigidbody2D cachedRigidbody;
    private Collider2D cachedCollider;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryCrush(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryCrush(collision);
    }

    void TryCrush(Collision2D collision)
    {
        if (collision == null || cachedRigidbody == null)
            return;

        if (cachedRigidbody.velocity.y > -minimumDownwardSpeed)
            return;

        PlayerMovement2D player = FindPlayer(collision);
        if (player == null)
            return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (requireHitFromAbove && !IsCrushingFromAbove(collision, playerCollider))
            return;

        IDamageable damageable = player.GetComponent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(damage);
        else
            player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    PlayerMovement2D FindPlayer(Collision2D collision)
    {
        if (collision.collider == null)
            return null;

        PlayerMovement2D player = collision.collider.GetComponent<PlayerMovement2D>();
        if (player != null)
            return player;

        return collision.collider.attachedRigidbody != null
            ? collision.collider.attachedRigidbody.GetComponent<PlayerMovement2D>()
            : collision.collider.GetComponentInParent<PlayerMovement2D>();
    }

    bool IsCrushingFromAbove(Collision2D collision, Collider2D playerCollider)
    {
        if (cachedCollider == null || playerCollider == null)
            return false;

        Bounds blockBounds = cachedCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;

        if (blockBounds.center.y <= playerBounds.center.y)
            return false;

        bool hasHorizontalOverlap = blockBounds.max.x > playerBounds.min.x && blockBounds.min.x < playerBounds.max.x;
        if (!hasHorizontalOverlap)
            return false;

        float topHitLine = playerBounds.center.y + topHitTolerance;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.point.y >= topHitLine)
                return true;
        }

        return blockBounds.min.y >= playerBounds.center.y - topHitTolerance;
    }
}
