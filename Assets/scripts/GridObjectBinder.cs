using UnityEngine;

public class GridObjectBinder : MonoBehaviour
{
    public WorldGrid worldGrid;

    [Header("这个物体在世界里占多大（Unity单位）")]
    public Vector2 sizeInUnits = Vector2.one;

    [Header("是否写入占用层")]
    public bool writeOccupants = true;

    [Header("写入占用层时是否把当前流体挤开")]
    public bool displaceFluids = true;

    void LateUpdate()
    {
        if (!writeOccupants) return;

        if (worldGrid == null)
            worldGrid = FindObjectOfType<WorldGrid>();

        if (worldGrid == null) return;

        WriteOccupants();
    }

    void WriteOccupants()
    {
        Vector2Int center = worldGrid.WorldToGrid(transform.position);

        int sizeX = Mathf.Max(1, Mathf.RoundToInt(sizeInUnits.x * worldGrid.CellsPerUnitX));
        int sizeY = Mathf.Max(1, Mathf.RoundToInt(sizeInUnits.y * worldGrid.CellsPerUnitY));

        worldGrid.FillOccupantRect(
            center,
            new Vector2Int(sizeX, sizeY),
            CellOccupantType.SolidObject,
            displaceFluids
        );
    }
}
