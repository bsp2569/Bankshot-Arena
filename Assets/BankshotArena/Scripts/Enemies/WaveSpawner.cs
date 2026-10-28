using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Endless waves. Enemies spawn evenly around a ring (polar coordinates) and get
/// more numerous and better at leading their shots each wave.
/// </summary>
public class WaveSpawner : MonoBehaviour
{
    public EnemyAI enemyPrefab;
    public Transform arenaCenter;
    public float spawnRadius = 24f;

    [Header("Difficulty")]
    public int baseEnemies = 3;
    public int extraEnemiesPerWave = 2;
    public int maxEnemiesPerWave = 14;
    public float timeBetweenWaves = 4f;
    public float leadAccuracyStart = 0.2f;
    public float leadAccuracyPerWave = 0.12f;

    public int CurrentWave { get; private set; }
    public int Alive { get; private set; }
    public float NextWaveCountdown { get; private set; }

    IEnumerator Start()
    {
        if (arenaCenter == null) arenaCenter = transform;
        var gm = GameManager.Instance;

        while (gm == null || !gm.IsGameOver)
        {
            NextWaveCountdown = timeBetweenWaves;
            while (NextWaveCountdown > 0f)
            {
                NextWaveCountdown -= Time.deltaTime;
                yield return null;
            }
            NextWaveCountdown = 0f;

            SpawnWave();
            yield return new WaitUntil(() => Alive <= 0 || (gm != null && gm.IsGameOver));
        }
    }

    void SpawnWave()
    {
        CurrentWave++;
        int count = Mathf.Min(maxEnemiesPerWave, baseEnemies + extraEnemiesPerWave * (CurrentWave - 1));
        float accuracy = Mathf.Clamp01(leadAccuracyStart + leadAccuracyPerWave * (CurrentWave - 1));

        float startAngle = Random.Range(0f, Mathf.PI * 2f); // rotate the ring each wave
        float step = Mathf.PI * 2f / count;                  // evenly spaced around 360°

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i + Random.Range(-0.2f, 0.2f) * step;
            Vector3 pos = GameMath.PointOnRing(arenaCenter.position, spawnRadius, angle);

            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas)) continue;

            Vector3 lookDir = arenaCenter.position - hit.position;
            lookDir.y = 0f;
            Quaternion rot = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

            EnemyAI enemy = Instantiate(enemyPrefab, hit.position, rot);
            enemy.leadAccuracy = accuracy;

            var health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                Alive++;
                health.OnDied += HandleEnemyDied;
            }
        }
    }

    void HandleEnemyDied(EnemyHealth enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        Alive = Mathf.Max(0, Alive - 1);
    }
}
