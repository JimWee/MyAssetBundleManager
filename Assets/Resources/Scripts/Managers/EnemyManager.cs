using UnityEngine;
using AssetBundles;

public class EnemyManager : MonoBehaviour
{
    public PlayerHealth playerHealth;
    //public GameObject enemy;
    public string enemyPath;
    public float spawnTime = 3f;
    public Transform[] spawnPoints;

    GameObject enemy;

    void Start ()
    {
        InvokeRepeating ("Spawn", spawnTime, spawnTime);
    }


    void Spawn ()
    {
        if(playerHealth.currentHealth <= 0f)
        {
            return;
        }

        int spawnPointIndex = Random.Range (0, spawnPoints.Length);

        if (enemy == null)
        {
            enemy = AssetBundleLoader.Instance.LoadAsset(enemyPath) as GameObject;
        }

        Instantiate (enemy, spawnPoints[spawnPointIndex].position, spawnPoints[spawnPointIndex].rotation);
    }
}
