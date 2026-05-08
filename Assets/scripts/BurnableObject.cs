using UnityEngine;

public class BurnableObject : MonoBehaviour
{
    public WorldGrid worldGrid;
    public Sprite fireSprite;

    [Header("Burning")]
    public float burnCheckInterval = 0.2f;
    public bool canBurn = true;
    public bool destroyOnBurn = true;
    public bool spreadFireOnBurn = false;
    public int spreadRadius = 1;
    public float burnDuration = 2f;
    public bool extinguishByWater = true;

    private float timer;
    private Collider2D cachedCollider;
    private ParticleSystem fireParticles;
    private Material fireParticleMaterial;
    private bool isBurning;
    private float burnEndTime;

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

        if (isBurning)
        {
            UpdateBurningState();
            return;
        }

        timer += Time.deltaTime;
        if (timer < burnCheckInterval)
            return;

        timer = 0f;
        if (!canBurn)
            return;

        if (IsTouchingFire() && destroyOnBurn)
            StartBurning();
    }

    void UpdateBurningState()
    {
        if (extinguishByWater && IsCoveredByWater())
        {
            StopBurning();
            return;
        }

        if (Time.time < burnEndTime)
            return;

        if (spreadFireOnBurn)
            SpreadFireNearby();

        Destroy(gameObject);
    }

    void StartBurning()
    {
        if (isBurning)
            return;

        isBurning = true;
        burnEndTime = Time.time + burnDuration;
        EnsureFireParticles();
        if (fireParticles != null && !fireParticles.isPlaying)
            fireParticles.Play();
    }

    void StopBurning()
    {
        isBurning = false;
        burnEndTime = 0f;

        if (fireParticles != null)
            fireParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy()
    {
        if (fireParticleMaterial != null)
            Destroy(fireParticleMaterial);
    }

    bool IsTouchingFire()
    {
        if (worldGrid == null)
            return false;

        if (cachedCollider == null)
        {
            Vector2Int gp = worldGrid.WorldToGrid(transform.position);
            return worldGrid.HasFireNearby(gp.x, gp.y);
        }

        Bounds bounds = cachedCollider.bounds;
        Vector3 insetMin = new Vector3(bounds.min.x + 0.02f, bounds.min.y + 0.02f, 0f);
        Vector3 insetMax = new Vector3(bounds.max.x - 0.02f, bounds.max.y - 0.02f, 0f);
        Vector2Int min = worldGrid.WorldToGrid(insetMin);
        Vector2Int max = worldGrid.WorldToGrid(insetMax);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                if (!worldGrid.InBounds(x, y))
                    continue;

                if (worldGrid.HasFireNearby(x, y))
                    return true;
            }
        }

        return false;
    }

    bool IsCoveredByWater()
    {
        if (worldGrid == null || cachedCollider == null)
            return false;

        Bounds bounds = cachedCollider.bounds;
        Vector3 insetMin = new Vector3(bounds.min.x + 0.02f, bounds.min.y + 0.02f, 0f);
        Vector3 insetMax = new Vector3(bounds.max.x - 0.02f, bounds.max.y - 0.02f, 0f);
        Vector2Int min = worldGrid.WorldToGrid(insetMin);
        Vector2Int max = worldGrid.WorldToGrid(insetMax);

        int coveredCells = 0;
        int totalCells = 0;
        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                if (!worldGrid.InBounds(x, y))
                    continue;

                totalCells++;
                if (worldGrid.IsWaterCell(x, y))
                    coveredCells++;
            }
        }

        if (totalCells == 0)
            return false;

        return coveredCells >= Mathf.Max(1, totalCells / 3);
    }

    void SpreadFireNearby()
    {
        if (worldGrid == null)
            return;

        Vector2Int center = worldGrid.WorldToGrid(transform.position);
        for (int dx = -spreadRadius; dx <= spreadRadius; dx++)
        {
            for (int dy = -spreadRadius; dy <= spreadRadius; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > spreadRadius)
                    continue;

                int x = center.x + dx;
                int y = center.y + dy;
                worldGrid.TryIgniteOrSetFire(x, y);
            }
        }
    }

    void EnsureFireParticles()
    {
        if (fireParticles != null)
            return;

        GameObject particleObject = new GameObject("FireParticles");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = Vector3.zero;

        fireParticles = particleObject.AddComponent<ParticleSystem>();
        var main = fireParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.78f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.14f, 0.42f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.42f, 0.08f, 0.95f),
            new Color(0.95f, 0.12f, 0.04f, 0.88f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        var emission = fireParticles.emission;
        emission.rateOverTime = 11f;

        var shape = fireParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        Vector3 size = cachedCollider != null ? cachedCollider.bounds.size : Vector3.one * 0.5f;
        shape.scale = new Vector3(Mathf.Max(0.08f, size.x * 0.34f), Mathf.Max(0.04f, size.y * 0.1f), 0.05f);
        shape.position = new Vector3(0f, Mathf.Max(0f, size.y * 0.08f), 0f);

        var velocityOverLifetime = fireParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.52f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);

        var noise = fireParticles.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;

        var colorOverLifetime = fireParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.82f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.32f, 0.08f), 0.35f),
                new GradientColorKey(new Color(0.85f, 0.08f, 0.04f), 0.72f),
                new GradientColorKey(new Color(0.18f, 0.16f, 0.16f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.92f, 0f),
                new GradientAlphaKey(0.86f, 0.35f),
                new GradientAlphaKey(0.72f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = fireParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.7f);
        sizeCurve.AddKey(0.4f, 1f);
        sizeCurve.AddKey(1f, 0.16f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = fireParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 40;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ApplyFireSprite(renderer);
    }

    void ApplyFireSprite(ParticleSystemRenderer renderer)
    {
        if (fireSprite == null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        fireParticleMaterial = new Material(shader);
        fireParticleMaterial.mainTexture = fireSprite.texture;

        Rect rect = fireSprite.textureRect;
        Vector2 textureSize = new Vector2(fireSprite.texture.width, fireSprite.texture.height);
        fireParticleMaterial.mainTextureScale = new Vector2(rect.width / textureSize.x, rect.height / textureSize.y);
        fireParticleMaterial.mainTextureOffset = new Vector2(rect.x / textureSize.x, rect.y / textureSize.y);

        renderer.material = fireParticleMaterial;
    }
}
