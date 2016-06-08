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
        if (AssetBundleLoader.TestUsedResourcesLoad)
        {
            GameObject obj = Resources.Load(enemyPath) as GameObject;
        }

        if (AssetBundleLoader.TestPreLoad)
        {
            enemy = AssetBundleLoader.Instance.LoadAsset(enemyPath) as GameObject;
            if (AssetBundleLoader.TestUnload)
            {
                AssetBundleLoader.Instance.UnloadAsset(enemyPath);
            }
        }
        if (AssetBundleLoader.TestUsedLoad)
        {
            GameObject obj = AssetBundleLoader.Instance.LoadAsset(enemyPath) as GameObject;
            if (AssetBundleLoader.TestUnload)
            {
                AssetBundleLoader.Instance.UnloadAsset(enemyPath);
            }
        }
        InvokeRepeating ("Spawn", spawnTime, spawnTime);
    }


    void Spawn ()
    {
        if(playerHealth.currentHealth <= 0f)
        {
            return;
        }

        int spawnPointIndex = Random.Range (0, spawnPoints.Length);

        if (AssetBundleLoader.TestResourcesLoad)
        {
            if (enemy == null)
            {
                enemy = Resources.Load(enemyPath) as GameObject;
            }
        }
        else
        {
            if (enemy == null && !AssetBundleLoader.TestPreLoad)
            {
                enemy = AssetBundleLoader.Instance.LoadAsset(enemyPath) as GameObject;
                if (AssetBundleLoader.TestUnload)
                {
                    AssetBundleLoader.Instance.UnloadAsset(enemyPath);
                }
            }
        }

        Instantiate (enemy, spawnPoints[spawnPointIndex].position, spawnPoints[spawnPointIndex].rotation);
    }
}
