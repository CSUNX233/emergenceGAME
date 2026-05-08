using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BalanceScale : MonoBehaviour
{
    [Header("Shape")]
    public float beamWidth = 4.4f;
    public float basketWidth = 1.15f;
    public float basketWallHeight = 0.72f;
    public float basketDrop = 0.58f;

    [Header("Balance")]
    public float degreesPerWeightDifference = 7f;
    public float maxTiltDegrees = 28f;
    public float tiltSmoothTime = 0.22f;
    public LayerMask weightLayers = ~0;

    private Transform beamRoot;
    private Rigidbody2D beamBody;
    private readonly Collider2D[] overlapHits = new Collider2D[48];
    private readonly HashSet<int> countedObjects = new HashSet<int>();
    private float currentTilt;
    private float tiltVelocity;

    public static GameObject CreateRuntimePrefab()
    {
        GameObject prefab = new GameObject("BalanceScale");
        prefab.SetActive(false);

        int placeBlockLayer = LayerMask.NameToLayer("PlaceBlock");
        if (placeBlockLayer >= 0)
            prefab.layer = placeBlockLayer;

        GridObjectBinder binder = prefab.AddComponent<GridObjectBinder>();
        binder.sizeInUnits = new Vector2(4.8f, 1.9f);
        binder.writeOccupants = false;

        BalanceScale scale = prefab.AddComponent<BalanceScale>();
        scale.weightLayers = ~0;
        return prefab;
    }

    void Awake()
    {
        EnsureBuilt();
    }

    void FixedUpdate()
    {
        EnsureBuilt();

        float leftWeight = SampleBasketWeight(-GetBasketCenterX());
        float rightWeight = SampleBasketWeight(GetBasketCenterX());
        float targetTilt = Mathf.Clamp(
            (leftWeight - rightWeight) * degreesPerWeightDifference,
            -maxTiltDegrees,
            maxTiltDegrees
        );

        currentTilt = Mathf.SmoothDampAngle(currentTilt, targetTilt, ref tiltVelocity, tiltSmoothTime);
        if (beamBody != null)
            beamBody.MoveRotation(transform.eulerAngles.z + currentTilt);
    }

    void EnsureBuilt()
    {
        if (beamRoot != null)
            return;

        beamRoot = new GameObject("BeamAndBaskets").transform;
        beamRoot.SetParent(transform, false);
        beamRoot.localPosition = Vector3.zero;

        beamBody = beamRoot.gameObject.AddComponent<Rigidbody2D>();
        beamBody.bodyType = RigidbodyType2D.Kinematic;
        beamBody.gravityScale = 0f;
        beamBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        beamBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CopyLayerRecursively(transform.gameObject.layer);
        BuildStand();
        BuildBeamAndBaskets();
    }

    void BuildStand()
    {
        CreateVisualBox("Base", transform, new Vector2(0f, -0.95f), new Vector2(1.25f, 0.12f), new Color(0.42f, 0.37f, 0.31f), false);
        CreateVisualBox("Post", transform, new Vector2(0f, -0.42f), new Vector2(0.12f, 1.0f), new Color(0.46f, 0.40f, 0.34f), false);
        CreateVisualBox("Pivot", transform, new Vector2(0f, 0.12f), new Vector2(0.34f, 0.18f), new Color(0.72f, 0.64f, 0.43f), false);
    }

    void BuildBeamAndBaskets()
    {
        CreateVisualBox("Beam", beamRoot, new Vector2(0f, 0f), new Vector2(beamWidth, 0.09f), new Color(0.60f, 0.48f, 0.34f), true);

        float basketCenterX = GetBasketCenterX();
        BuildBasket("LeftBasket", -basketCenterX);
        BuildBasket("RightBasket", basketCenterX);
    }

    void BuildBasket(string namePrefix, float centerX)
    {
        float halfWidth = basketWidth * 0.5f;
        float sideInset = 0.04f;
        float bottomY = -basketDrop;
        float wallCenterY = bottomY + basketWallHeight * 0.5f;

        CreateVisualBox(namePrefix + "RopeA", beamRoot, new Vector2(centerX - halfWidth * 0.38f, -0.25f), new Vector2(0.035f, 0.52f), new Color(0.50f, 0.44f, 0.36f), false);
        CreateVisualBox(namePrefix + "RopeB", beamRoot, new Vector2(centerX + halfWidth * 0.38f, -0.25f), new Vector2(0.035f, 0.52f), new Color(0.50f, 0.44f, 0.36f), false);
        CreateVisualBox(namePrefix + "Bottom", beamRoot, new Vector2(centerX, bottomY), new Vector2(basketWidth, 0.08f), new Color(0.55f, 0.47f, 0.36f), true);
        CreateVisualBox(namePrefix + "LeftWall", beamRoot, new Vector2(centerX - halfWidth + sideInset, wallCenterY), new Vector2(0.08f, basketWallHeight), new Color(0.55f, 0.47f, 0.36f), true);
        CreateVisualBox(namePrefix + "RightWall", beamRoot, new Vector2(centerX + halfWidth - sideInset, wallCenterY), new Vector2(0.08f, basketWallHeight), new Color(0.55f, 0.47f, 0.36f), true);
    }

    GameObject CreateVisualBox(string objectName, Transform parent, Vector2 localPosition, Vector2 size, Color color, bool withCollider)
    {
        GameObject box = new GameObject(objectName);
        box.layer = gameObject.layer;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = box.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSolidSprite();
        renderer.color = color;
        renderer.sortingOrder = 12;

        if (withCollider)
        {
            BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }

        return box;
    }

    float SampleBasketWeight(float localCenterX)
    {
        if (beamRoot == null)
            return 0f;

        countedObjects.Clear();

        Vector2 localCenter = new Vector2(localCenterX, -basketDrop + basketWallHeight * 0.46f);
        Vector2 worldCenter = beamRoot.TransformPoint(localCenter);
        Vector2 size = new Vector2(Mathf.Max(0.1f, basketWidth - 0.18f), basketWallHeight + 0.22f);
        int hitCount = Physics2D.OverlapBoxNonAlloc(worldCenter, size, beamRoot.eulerAngles.z, overlapHits, weightLayers);

        float totalWeight = 0f;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            Rigidbody2D body = hit.attachedRigidbody;
            if (body != null && body == beamBody)
                continue;

            int id = body != null ? body.GetInstanceID() : hit.gameObject.GetInstanceID();
            if (!countedObjects.Add(id))
                continue;

            totalWeight += GetWeight(hit, body);
        }

        return totalWeight;
    }

    float GetWeight(Collider2D hit, Rigidbody2D body)
    {
        PhysicalWeight weight = hit.GetComponentInParent<PhysicalWeight>();
        if (weight != null)
            return weight.CurrentWeight;

        if (body != null)
            return Mathf.Max(0.05f, body.mass);

        return 1f;
    }

    float GetBasketCenterX()
    {
        return beamWidth * 0.5f - basketWidth * 0.62f;
    }

    void CopyLayerRecursively(int layer)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    static Sprite solidSprite;

    static Sprite GetSolidSprite()
    {
        if (solidSprite != null)
            return solidSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.DontSave;

        solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        solidSprite.hideFlags = HideFlags.DontSave;
        return solidSprite;
    }
}
