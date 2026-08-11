using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("Ball")]
    [SerializeField] private GameObject ballPrefab;

    private GameObject currentBall;

    public GameObject CurrentBall => currentBall;

    public GameObject SpawnBall(Transform spawnPoint)
    {
        if (ballPrefab == null)
        {
            Debug.LogError("[BallSpawner] Ball Prefab が設定されていません。");
            return null;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("[BallSpawner] Spawn Point がnullです。");
            return null;
        }

        currentBall = Instantiate(
            ballPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        Debug.Log(
            $"[BallSpawner] Spawned at {spawnPoint.name} / {spawnPoint.position}"
        );

        return currentBall;
    }

    public GameObject RespawnBall(Transform spawnPoint)
    {
        DestroyCurrentBall();
        return SpawnBall(spawnPoint);
    }

    public void DestroyCurrentBall()
    {
        if (currentBall == null)
            return;

        Destroy(currentBall);
        currentBall = null;
    }
}