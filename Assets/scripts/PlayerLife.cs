using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement2D))]
public class PlayerLife : MonoBehaviour, IDamageable
{
    public float maxHealth = 1f;
    public float fallDeathDistance = 14f;
    public float respawnDelay = 0.45f;

    private Rigidbody2D cachedRigidbody;
    private Collider2D cachedCollider;
    private PlayerMovement2D cachedMovement;
    private float health;
    private bool isDead;
    private float nextRespawnTime;

    public Vector3 SpawnPoint { get; private set; }
    public bool IsDead => isDead;

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedCollider = GetComponent<Collider2D>();
        cachedMovement = GetComponent<PlayerMovement2D>();
        SpawnPoint = transform.position;
        health = maxHealth;
    }

    void Update()
    {
        if (!isDead && transform.position.y < SpawnPoint.y - fallDeathDistance)
            Die();

        if (isDead && Time.time >= nextRespawnTime)
            LevelManager.Instance?.ReloadCurrentLevelAfterDeath();
    }

    public void SetSpawnPoint(Vector3 spawnPoint)
    {
        SpawnPoint = spawnPoint;
    }

    public void ResetForLevel(Vector3 spawnPoint)
    {
        SpawnPoint = spawnPoint;
        isDead = false;
        health = maxHealth;
        transform.position = spawnPoint;
        transform.rotation = Quaternion.identity;

        if (cachedRigidbody != null)
        {
            cachedRigidbody.velocity = Vector2.zero;
            cachedRigidbody.angularVelocity = 0f;
            cachedRigidbody.simulated = true;
        }

        if (cachedCollider != null)
            cachedCollider.enabled = true;

        if (cachedMovement != null)
            cachedMovement.enabled = true;
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        health -= Mathf.Max(0f, damage);
        if (health <= 0f)
            Die();
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        nextRespawnTime = Time.time + respawnDelay;

        if (cachedMovement != null)
            cachedMovement.enabled = false;

        if (cachedRigidbody != null)
            cachedRigidbody.velocity = Vector2.zero;

        LevelManager.Instance?.HandlePlayerDied(this);
    }
}
