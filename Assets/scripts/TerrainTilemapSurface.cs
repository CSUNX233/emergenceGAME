using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class TerrainTilemapSurface : MonoBehaviour
{
    public int attachSearchRadiusCells = 3;

    private Tilemap cachedTilemap;

    void Awake()
    {
        cachedTilemap = GetComponent<Tilemap>();
    }

    void OnEnable()
    {
        if (cachedTilemap == null)
            cachedTilemap = GetComponent<Tilemap>();
    }

    public bool TryGetNearestFacePose(Vector3 desiredPosition, Vector2 itemLocalSize, Vector2 itemLocalOffset, out SpikeTrap.AttachmentPose pose)
    {
        pose = default;
        if (cachedTilemap == null)
            return false;

        Vector3Int centerCell = cachedTilemap.WorldToCell(desiredPosition);
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int x = -attachSearchRadiusCells; x <= attachSearchRadiusCells; x++)
        {
            for (int y = -attachSearchRadiusCells; y <= attachSearchRadiusCells; y++)
            {
                Vector3Int cell = centerCell + new Vector3Int(x, y, 0);
                if (!cachedTilemap.HasTile(cell))
                    continue;

                TrySurfaceRun(cell, Vector3Int.up, 0f, desiredPosition, itemLocalSize, itemLocalOffset, ref pose, ref bestDistance, ref found);
                TrySurfaceRun(cell, Vector3Int.down, 180f, desiredPosition, itemLocalSize, itemLocalOffset, ref pose, ref bestDistance, ref found);
                TrySurfaceRun(cell, Vector3Int.right, -90f, desiredPosition, itemLocalSize, itemLocalOffset, ref pose, ref bestDistance, ref found);
                TrySurfaceRun(cell, Vector3Int.left, 90f, desiredPosition, itemLocalSize, itemLocalOffset, ref pose, ref bestDistance, ref found);
            }
        }

        return found;
    }

    void TrySurfaceRun(
        Vector3Int cell,
        Vector3Int direction,
        float rotationZ,
        Vector3 desiredPosition,
        Vector2 itemLocalSize,
        Vector2 itemLocalOffset,
        ref SpikeTrap.AttachmentPose bestPose,
        ref float bestDistance,
        ref bool found)
    {
        if (!IsExposedFace(cell, direction))
            return;

        Vector3Int tangent = direction.x == 0 ? Vector3Int.right : Vector3Int.up;
        Vector3Int runStart = cell;
        Vector3Int runEnd = cell;
        while (IsExposedFace(runStart - tangent, direction))
            runStart -= tangent;
        while (IsExposedFace(runEnd + tangent, direction))
            runEnd += tangent;

        Vector3 cellCenter = cachedTilemap.GetCellCenterWorld(cell);
        Vector3 cellSize = cachedTilemap.layoutGrid != null ? cachedTilemap.layoutGrid.cellSize : Vector3.one;
        Vector2 itemHalfSize = GetRotatedSize(itemLocalSize, rotationZ) * 0.5f;
        Vector2 rotatedOffset = Rotate(itemLocalOffset, rotationZ);

        Vector2 tileHalfSize = new Vector2(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)) * 0.5f;
        Vector2 normal = new Vector2(direction.x, direction.y);

        Vector2 colliderCenter;
        if (direction.x == 0)
        {
            float minX = cachedTilemap.GetCellCenterWorld(runStart).x - tileHalfSize.x;
            float maxX = cachedTilemap.GetCellCenterWorld(runEnd).x + tileHalfSize.x;
            float clampedX = Mathf.Clamp(desiredPosition.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            float surfaceY = cellCenter.y + normal.y * (tileHalfSize.y + itemHalfSize.y);
            colliderCenter = new Vector2(clampedX, surfaceY);
        }
        else
        {
            float minY = cachedTilemap.GetCellCenterWorld(runStart).y - tileHalfSize.y;
            float maxY = cachedTilemap.GetCellCenterWorld(runEnd).y + tileHalfSize.y;
            float clampedY = Mathf.Clamp(desiredPosition.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
            float surfaceX = cellCenter.x + normal.x * (tileHalfSize.x + itemHalfSize.x);
            colliderCenter = new Vector2(surfaceX, clampedY);
        }

        Vector2 objectPosition = colliderCenter - rotatedOffset;
        float distance = Vector2.Distance(desiredPosition, objectPosition);

        if (found && distance >= bestDistance)
            return;

        found = true;
        bestDistance = distance;
        bestPose = new SpikeTrap.AttachmentPose
        {
            position = new Vector3(objectPosition.x, objectPosition.y, 0f),
            rotationZ = rotationZ
        };
    }

    bool IsExposedFace(Vector3Int cell, Vector3Int direction)
    {
        return cachedTilemap.HasTile(cell) && !cachedTilemap.HasTile(cell + direction);
    }

    static Vector2 GetRotatedSize(Vector2 size, float rotationZ)
    {
        int quarterTurns = Mathf.RoundToInt(Mathf.Repeat(rotationZ, 360f) / 90f) % 4;
        return quarterTurns % 2 == 0 ? size : new Vector2(size.y, size.x);
    }

    static Vector2 Rotate(Vector2 value, float rotationZ)
    {
        float radians = rotationZ * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos);
    }
}
