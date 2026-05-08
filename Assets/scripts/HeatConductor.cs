using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HeatConductor : MonoBehaviour
{
    public WorldGrid worldGrid;
    public float heatCheckInterval = 0.15f;
    public float heatHoldTime = 1.2f;
    public int conductionRadius = 1;

    private Collider2D cachedCollider;
    private float heatTimer;
    private float hotUntilTime;

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (worldGrid == null)
            worldGrid = FindObjectOfType<WorldGrid>();
        if (worldGrid == null)
            return;

        heatTimer += Time.deltaTime;
        if (heatTimer < heatCheckInterval)
            return;

        heatTimer = 0f;
        if (TouchesFire())
            hotUntilTime = Time.time + heatHoldTime;

        if (Time.time <= hotUntilTime)
            ConductHeat();
    }

    bool TouchesFire()
    {
        Bounds bounds = cachedCollider.bounds;
        Vector2Int min = worldGrid.WorldToGrid(bounds.min);
        Vector2Int max = worldGrid.WorldToGrid(bounds.max);

        for (int x = min.x - 1; x <= max.x + 1; x++)
        {
            for (int y = min.y - 1; y <= max.y + 1; y++)
            {
                if (worldGrid.HasFireNearby(x, y))
                    return true;
            }
        }

        return false;
    }

    void ConductHeat()
    {
        Bounds bounds = cachedCollider.bounds;
        Vector2Int min = worldGrid.WorldToGrid(bounds.min);
        Vector2Int max = worldGrid.WorldToGrid(bounds.max);

        for (int x = min.x - conductionRadius; x <= max.x + conductionRadius; x++)
        {
            for (int y = min.y - conductionRadius; y <= max.y + conductionRadius; y++)
            {
                bool onRing = x < min.x || x > max.x || y < min.y || y > max.y;
                if (!onRing)
                    continue;

                worldGrid.TryIgniteOrSetFire(x, y);
            }
        }
    }
}
