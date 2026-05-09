using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelCollectionData
{
    public int activeLevelIndex;
    public List<LevelData> levels = new List<LevelData>();
}

[Serializable]
public class LevelData
{
    public string name;
    public string terrainPrefabId;
    public SerializableVector3 playerSpawn;
    public List<PlacedObjectData> placedObjects = new List<PlacedObjectData>();
    public List<MaterialCellData> materialCells = new List<MaterialCellData>();
}

[Serializable]
public class PlacedObjectData
{
    public BuildTool tool;
    public SerializableVector3 position;
    public float rotationZ;
}

[Serializable]
public class MaterialCellData
{
    public int x;
    public int y;
    public MaterialType material;
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(Vector3 value)
    {
        x = value.x;
        y = value.y;
        z = value.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
