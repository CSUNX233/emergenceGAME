using UnityEngine;

[DisallowMultipleComponent]
public class LevelPlacedObject : MonoBehaviour
{
    public BuildTool tool;

    public PlacedObjectData ToData()
    {
        return new PlacedObjectData
        {
            tool = tool,
            position = new SerializableVector3(transform.position),
            rotationZ = transform.eulerAngles.z
        };
    }
}
