using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using AssetBundles;

public class GameManager : MonoBehaviour
{

    public GameObject UIPopMsg;
    public GameObject UIProgressBar;
    public GameObject UIBottomMsg;

    GameObject mCube;

    // Use this for initialization
    IEnumerator Start()
    {
        string error = null;

        AssetBundleUpdate.SetSourceAssetBundleURL(AssetBundleUpdate.GetAssetBundleServerUrl());

        //检查本地是否存在version文件
        string localVersinFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName);
        if (File.Exists(localVersinFilePath))
        {
            Debug.Log("检查更新");
            SetBottomMsg("检查更新");

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
            SetBottomMsg("首次运行游戏，需下载初始游戏资源");
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

        Debug.Log(string.Format("更新文件{0}个，大小{1:F}MB", assetBundlesCount, assetBundlesSize / (1024 * 1024)));

        //下载AssetBundles
        int downloadCount = 0;
        float downloadSize = 0;
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
            downloadCount++;
            downloadSize += item.Value.Size;

            SetBottomMsg(string.Format("数量：{0}/{1}  大小：{2:F}KB/{3:F}KB", downloadCount, assetBundlesCount, downloadSize / 1024, assetBundlesSize / 1024));
            UpdateProgress(downloadSize / assetBundlesSize);
        }

        //保存version文件
        if (!AssetBundleUpdate.SaveFile(AssetBundleUtility.VersionFileName, out error))
        {
            Debug.Log(error);
            yield break;
        }

        Debug.Log("更新完成");
        SetBottomMsg("更新完成");

        //加载AssetBundleLoader
        Debug.Log("初始化AssetBundleLoader");
        SetBottomMsg("初始化资源");
        gameObject.AddComponent<AssetBundleLoader>();
        yield return StartCoroutine(AssetBundleLoader.Instance.Init());
        Debug.Log("初始化AssetBundleLoader完成");
        SetBottomMsg("初始化资源完成");
    }

    // Update is called once per frame
    void Update()
    {

    }

    void SetBottomMsg(string text)
    {
        Text textCpt = UIBottomMsg.GetComponent<Text>();
        textCpt.text = text;
    }

    void UpdateProgress(float value)
    {
        Text textCpt = UIProgressBar.transform.Find("Text").GetComponent<Text>();
        textCpt.text = string.Format("{0:F}%", value * 100);

        Slider sliderCpt = UIProgressBar.GetComponent<Slider>();
        sliderCpt.value = value;
    }

    public void LoadCubeAsync()
    {
        StartCoroutine(AssetBundleLoader.Instance.LoadAssetAsync("prefab/mycube", delegate (Object asset) { mCube = Instantiate(asset) as GameObject; }));
    }

    public void LoadCube()
    {
        mCube = Instantiate(AssetBundleLoader.Instance.LoadAsset("prefab/mycube")) as GameObject;
    }

    public void DestroyCube()
    {
        Destroy(mCube);
        Resources.UnloadUnusedAssets();
    }

    public void UnloadCube()
    {
        AssetBundleLoader.Instance.UnloadAsset("prefab/mycube");
    }
}
