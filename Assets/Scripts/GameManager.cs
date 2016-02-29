using UnityEngine;
using System.Collections;
using System;
using System.IO;
using AssetBundles;

public class GameManager : MonoBehaviour
{

    // Use this for initialization
    IEnumerator Start()
    {
        string error = null;

        //检查本地是否存在version文件
        string localVersinFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName);
        if (File.Exists(localVersinFilePath))
        {
            Debug.Log("检查更新");

            //解析本地version文件
            byte[] localVersionBytes = File.ReadAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName));
            if (!AssetBundleUtility.ResolveVersionData(localVersionBytes, ref AssetBundleUpdate.LocalAssetBundleInfos, out error))
            {
                Debug.Log(error);
                yield break;
            }
        }
        else
        {
            Debug.Log("首次运行游戏，需下载初始游戏资源");
        }


        //下载version文件
        yield return StartCoroutine(AssetBundleUpdate.DownloadFile(AssetBundleUtility.VersionFileName));
        if (AssetBundleUpdate.GetDownloadingError(AssetBundleUtility.VersionFileName, out error))
        {
            Debug.Log(string.Format("下载资源失败：{0}", error));
            yield break;
        }

        //解析服务器version文件
        WWW wwwVersion;
        if (AssetBundleUpdate.GetDoneWWW(AssetBundleUtility.VersionFileName, out wwwVersion))
        {
            if (!AssetBundleUtility.ResolveVersionData(wwwVersion.bytes, ref AssetBundleUpdate.ServerAssetBundleInfos, out error))
            {
                Debug.Log(error);
                yield break;
            }
        }
        else
        {
            Debug.Log(string.Format("获取WWW失败：{0}", AssetBundleUtility.VersionFileName));
            yield break;
        }

        //比较版本信息，确定下载文件
        int assetBundlesCount = 0;
        float assetBundlesSize = 0;
        AssetBundleUpdate.SetDownloadAssetBundleInfos();
        AssetBundleUpdate.FilterDownloadAssetBundleInfos();
        AssetBundleUpdate.GetDowloadInfo(out assetBundlesCount, out assetBundlesSize);

        Debug.Log(string.Format("更新文件{0}个，大小{1}B", assetBundlesCount, assetBundlesSize / (1024 * 1024)));

        //下载AssetBundles
        foreach (var item in AssetBundleUpdate.DowloadAssetBundleInfos)
        {
            yield return StartCoroutine(AssetBundleUpdate.DownloadFile(item.Value.AssetBundleName));
            if (AssetBundleUpdate.GetDownloadingError(item.Value.AssetBundleName, out error))
            {
                Debug.Log(string.Format("下载资源失败：{0}", error));
                yield break;
            }
            if (!AssetBundleUpdate.SaveFile(item.Value.AssetBundleName, out error))
            {
                Debug.Log(error);
                yield break;
            }
        }

        //保存version文件
        if (!AssetBundleUpdate.SaveFile(AssetBundleUtility.VersionFileName, out error))
        {
            Debug.Log(error);
            yield break;
        }

        Debug.Log("更新完成");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
