using UnityEngine;

public static class PlacementSizeUtility
{
    public static Vector2 GetHalfSize(GameObject obj)
    {
        if (obj == null)
            return Vector2.one * 0.5f;

        GridObjectBinder binder = obj.GetComponent<GridObjectBinder>();
        if (binder != null)
            return new Vector2(Mathf.Abs(binder.sizeInUnits.x), Mathf.Abs(binder.sizeInUnits.y)) * 0.5f;

        Collider2D collider = obj.GetComponent<Collider2D>();
        if (collider != null)
            return collider.bounds.extents;

        SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            return spriteRenderer.bounds.extents;

        return Vector2.one * 0.5f;
    }

    public static Vector2 GetHalfSize(Collider2D collider)
    {
        if (collider == null)
            return Vector2.one * 0.5f;

        GridObjectBinder binder = collider.GetComponent<GridObjectBinder>();
        if (binder != null)
            return new Vector2(Mathf.Abs(binder.sizeInUnits.x), Mathf.Abs(binder.sizeInUnits.y)) * 0.5f;

        return collider.bounds.extents;
    }

    public static Vector2 GetPlacementCheckSize(GameObject obj)
    {
        if (obj != null && obj.GetComponent<FlagGoal>() != null)
        {
            Vector2 colliderSize = GetColliderScaledSize(obj);
            if (colliderSize != Vector2.zero)
                return colliderSize * 0.98f;
        }

        GridObjectBinder binder = obj != null ? obj.GetComponent<GridObjectBinder>() : null;
        if (binder != null)
            return new Vector2(Mathf.Abs(binder.sizeInUnits.x), Mathf.Abs(binder.sizeInUnits.y)) * 0.95f;

        if (obj == null)
            return Vector2.one * 0.9f;

        BoxCollider2D box = obj.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Vector3 scale = obj.transform.localScale;
            return new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y)) * 0.95f;
        }

        CapsuleCollider2D capsule = obj.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            Vector3 scale = obj.transform.localScale;
            return new Vector2(Mathf.Abs(capsule.size.x * scale.x), Mathf.Abs(capsule.size.y * scale.y)) * 0.95f;
        }

        CircleCollider2D circle = obj.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            float diameter = circle.radius * 2f;
            Vector3 scale = obj.transform.localScale;
            return new Vector2(Mathf.Abs(diameter * scale.x), Mathf.Abs(diameter * scale.y)) * 0.95f;
        }

        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.bounds.size * 0.95f;

        return Vector2.one * 0.9f;
    }

    static Vector2 GetColliderScaledSize(GameObject obj)
    {
        BoxCollider2D box = obj.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Vector3 scale = obj.transform.lossyScale;
            return new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y));
        }

        CapsuleCollider2D capsule = obj.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            Vector3 scale = obj.transform.lossyScale;
            return new Vector2(Mathf.Abs(capsule.size.x * scale.x), Mathf.Abs(capsule.size.y * scale.y));
        }

        CircleCollider2D circle = obj.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Vector3 scale = obj.transform.lossyScale;
            float diameter = circle.radius * 2f;
            return new Vector2(Mathf.Abs(diameter * scale.x), Mathf.Abs(diameter * scale.y));
        }

        return Vector2.zero;
    }
}
