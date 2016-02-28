using UnityEngine;
using System.Collections;
using System;
using System.IO;
using AssetBundles;

public class GameManager : MonoBehaviour
{

    // Use this for initialization
    void Start()
    {
        //检查本地是否存在version文件
        if (File.Exists(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName)))
        {
            Debug.Log("检查更新");
        }
        else
        {
            Debug.Log("首次运行游戏，需下载初始游戏资源");

        }

        StartCoroutine(AssetBundleDownloader.LoadLocalAssetBundleManifestAsync());
    }

    // Update is called once per frame
    void Update()
    {

    }
}
