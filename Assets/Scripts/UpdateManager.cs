using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Threading;
using AssetBundles;

public class UpdateManager : MonoBehaviour
{

    public GameObject UIPopMsg;
    public GameObject UIProgressBar;
    public GameObject UIBottomMsg;

    GameObject mCube;
    string mError = string.Empty;
    bool mIsDecompressing = false;
    int mZipFileNumber = 0;
    int[] mDecompressProgress = new int[1];

    /// <summary>
    /// 检查LocalAssetBundlePath路径下是否有VersionFileName文件
    ///     有=>比较本地和服务器资源差异，更新资源
    ///     无=>检查streamingAssetsPath路径下是否有ZipFileName
    ///         有=>解包到LocalAssetBundlePath，检查资源更新
    ///         无=>下载ZipFileName解包，检查资源更新
    /// </summary>
    /// <returns></returns>
    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (AssetBundleUtility.SimulateAssetBundleInEditor)
        {
            yield break;
        }        
#endif

        AssetBundleUpdate.SetSourceAssetBundleURL(AssetBundleUpdate.GetAssetBundleServerUrl());

        //检查本地是否存在version文件
        if (AssetBundleUpdate.ResolveLocalVersionFile() < 0)//没有本地版本文件信息
        {
            string zipFilePath = Path.Combine(Application.streamingAssetsPath, AssetBundleUtility.ZipFileName);
            if (!File.Exists(zipFilePath))
            {
                SetBottomMsg("首次运行游戏，下载游戏资源包");
                yield return StartCoroutine(AssetBundleUpdate.DownloadFile(AssetBundleUtility.ZipFileName));
                if (AssetBundleUpdate.GetDownloadingError(AssetBundleUtility.ZipFileName, out mError))
                {
                    SetBottomMsg("下载游戏资源包失败：" + mError);
                    yield break;
                }
                if (!AssetBundleUpdate.SaveFile(AssetBundleUtility.ZipFileName, out mError))
                {
                    SetBottomMsg(string.Format("{0}保存文件失败：{1}", AssetBundleUtility.ZipFileName, mError));
                    yield break;
                }
                AssetBundleUpdate.RemoveDoneWWW(AssetBundleUtility.ZipFileName);
                zipFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.ZipFileName);             
            }
            SetBottomMsg("解压游戏资源包");

            mZipFileNumber = lzip.getTotalFiles(zipFilePath);
            mIsDecompressing = true;
            int res = 0;
            Thread th = new Thread
                (() =>
                {
                    res = lzip.decompress_File(zipFilePath, AssetBundleUtility.LocalAssetBundlePath, mDecompressProgress);
                    mIsDecompressing = false;
                });
            th.Start();
            while (mIsDecompressing)
            {
                yield return new WaitForSeconds(1);
            }
            if (res < 0)
            {
                SetBottomMsg(string.Format("解压资源失败：{0}", res));
                yield break;
            }
        }


        //下载version文件
        yield return StartCoroutine(AssetBundleUpdate.DownloadFile(AssetBundleUtility.VersionFileName));
        if (AssetBundleUpdate.GetDownloadingError(AssetBundleUtility.VersionFileName, out mError))
        {
            Debug.Log(string.Format("下载资源失败：{0}", mError));
            yield break;
        }

        //解析服务器version文件
        WWW wwwVersion;
        if (AssetBundleUpdate.GetDoneWWW(AssetBundleUtility.VersionFileName, out wwwVersion))
        {
            if (!AssetBundleUtility.ResolveEncryptedVersionData(wwwVersion.bytes, ref AssetBundleUpdate.ServerAssetBundleInfos, out mError))
            {
                Debug.Log(mError);
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
            yield return StartCoroutine(AssetBundleUpdate.DownloadFile(item.Value.MD5));
            if (AssetBundleUpdate.GetDownloadingError(item.Value.MD5, out mError))
            {
                Debug.Log(string.Format("下载资源失败：{0}", mError));
                yield break;
            }
            if (!AssetBundleUpdate.SaveFile(item.Value.MD5, out mError))
            {
                Debug.Log(mError);
                yield break;
            }
            downloadCount++;
            downloadSize += item.Value.Size;

            SetBottomMsg(string.Format("数量：{0}/{1}  大小：{2:F}KB/{3:F}KB", downloadCount, assetBundlesCount, downloadSize / 1024, assetBundlesSize / 1024));
            UpdateProgress(downloadSize / assetBundlesSize);
        }

        //保存version文件
        if (!AssetBundleUpdate.SaveFile(AssetBundleUtility.VersionFileName, out mError))
        {
            Debug.Log(mError);
            yield break;
        }

        SetBottomMsg("更新完成");
        AssetBundleUpdate.Clear();

        //加载AssetBundleLoader
        SetBottomMsg("初始化资源");
        yield return StartCoroutine(AssetBundleLoader.Instance.Init());
        SetBottomMsg("初始化资源完成");
    }

    // Update is called once per frame
    void Update()
    {
        if (AssetBundleUpdate.DownloadingWWW != null)
        {
            UpdateProgress(AssetBundleUpdate.DownloadingWWW.progress);
        }
        if (mIsDecompressing)
        {
            UpdateProgress(mDecompressProgress[0] / (float)mZipFileNumber);
        }
    }

    void SetBottomMsg(string text)
    {
        Debug.Log(text);
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

    public void LoadScene()
    {
        AssetBundleLoader.Instance.LoadScene("scene/Scene2");
    }

    public void LoadSceneAsync()
    {
        StartCoroutine(AssetBundleLoader.Instance.LoadSceneAsync("scene/Scene2"));
    }
}
