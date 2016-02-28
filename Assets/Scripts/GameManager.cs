using UnityEngine;
using System.Collections;
using AssetBundles;

public class GameManager : MonoBehaviour
{

    // Use this for initialization
    void Start()
    {
        StartCoroutine(AssetBundleDownloader.LoadLocalAssetBundleManifestAsync());
    }

    // Update is called once per frame
    void Update()
    {

    }
}
