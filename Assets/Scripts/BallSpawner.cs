using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    public GameObject ballPrefab;

    GameObject currentBall;

    Vector3 spawnPos = new Vector3(-0.555f, 2.23f, -1.634f);

    bool courtChanged = false;

    void Start()
    {
        SpawnBall();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            RespawnBall();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            courtChanged = !courtChanged;
            RespawnBall();
        }
    }

    void RespawnBall()
    {
        if (currentBall != null)
        {
            Destroy(currentBall);
        }

        SpawnBall();
    }

    void SpawnBall()
    {
        Vector3 pos = GetSpawnPosition();

        currentBall = Instantiate(ballPrefab, pos, Quaternion.identity);

        BallController bc = currentBall.GetComponent<BallController>();
        if (bc != null)
        {
            bc.SetCourtChanged(courtChanged);
        }
    }

    Vector3 GetSpawnPosition()
    {
        if (!courtChanged) return spawnPos;

        // xz平面で原点対称
        return new Vector3(-spawnPos.x, spawnPos.y, -spawnPos.z);
    }
}