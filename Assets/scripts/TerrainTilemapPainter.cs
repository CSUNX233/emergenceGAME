using UnityEngine;
using UnityEngine.EventSystems;

public class TerrainTilemapPainter : MonoBehaviour
{
    public TerrainTilemapSystem terrainSystem;
    public Camera mainCamera;
    public bool paintEnabled;
    public int brushRadius;
    public bool blockWhenPointerOverUi = true;

    void Awake()
    {
        if (terrainSystem == null)
            terrainSystem = GetComponent<TerrainTilemapSystem>();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (!paintEnabled || terrainSystem == null)
            return;

        if (blockWhenPointerOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        Vector3 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        Vector3Int center = terrainSystem.WorldToCell(world);
        bool filled = Input.GetMouseButton(0);

        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int y = -brushRadius; y <= brushRadius; y++)
            {
                if (x * x + y * y > brushRadius * brushRadius)
                    continue;

                terrainSystem.SetTerrainTile(center + new Vector3Int(x, y, 0), filled);
            }
        }
    }
}
