using UnityEngine;

public class OccupantManager : MonoBehaviour
{
    public WorldGrid worldGrid;

    void LateUpdate()
    {
        if (worldGrid == null)
            worldGrid = GetComponent<WorldGrid>();

        if (worldGrid == null) return;

        // 先清
        worldGrid.ClearOccupants();

        // GridObjectBinder 会在它们自己的 LateUpdate 继续写入
        // 所以这个脚本执行顺序最好比 GridObjectBinder 更早
    }
}