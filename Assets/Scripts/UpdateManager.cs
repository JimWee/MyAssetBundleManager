using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Threading;
using AssetBundles;
using System;

public class UpdateManager : MonoBehaviour
{

    public GameObject UIPopMsg;
    public GameObject UIProgressBar;
    public GameObject UIBottomMsg;

    GameObject mCube;
    string mFileName = string.Empty;
    string mError = string.Empty;
    long mFileSize = 0;
    DateTime mLastModified;
    bool mIsDownloading = false;
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
            if (!File.Exists(zipFilePath))//android下会有问题
            {
                SetBottomMsg("首次运行游戏，下载游戏资源包");
                yield return StartCoroutine(AssetBundleUpdate.GetRemoteFileInfo(AssetBundleUtility.ZipFileName, OnGetRemoteFileInfo));
                if (CheckError()) yield break;
                mIsDownloading = true;
                yield return StartCoroutine(AssetBundleUpdate.DownloadFile(AssetBundleUtility.ZipFileName, OnDownloadFileFinidhed, FileMode.Append, mFileSize, mLastModified));
                mIsDownloading = false;
                if (CheckError()) yield break;
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
        WWW wwwVersion = new WWW(AssetBundleUpdate.BaseDownloadingURL + AssetBundleUtility.VersionFileName);
        yield return wwwVersion;
        if (wwwVersion.error != null)
        {
            SetBottomMsg("下载资源列表失败");
            Debug.LogError("Version file download failed");
            yield break;
        }

        //解析服务器version文件        
        if (!AssetBundleUtility.ResolveEncryptedVersionData(wwwVersion.bytes, ref AssetBundleUpdate.ServerAssetBundleInfos, out mError))
        {
            Debug.Log(mError);
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
            yield return StartCoroutine(AssetBundleUpdate.DownloadFile(item.Value.MD5, OnDownloadFileFinidhed));
            if (CheckError()) yield break;
            downloadCount++;
            downloadSize += item.Value.Size;

            SetBottomMsg(string.Format("数量：{0}/{1}  大小：{2:F}KB/{3:F}KB", downloadCount, assetBundlesCount, downloadSize / 1024, assetBundlesSize / 1024));
            UpdateProgress(downloadSize / assetBundlesSize);
        }

        //保存version文件
        File.WriteAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName), wwwVersion.bytes);

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
        if (mIsDownloading)
        {
            UpdateProgress(AssetBundleUpdate.GetDownloadPorgress());
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
        StartCoroutine(AssetBundleLoader.Instance.LoadAssetAsync("prefab/mycube", delegate (UnityEngine.Object asset) { mCube = Instantiate(asset) as GameObject; }));
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

    bool CheckError()
    {
        if (!string.IsNullOrEmpty(mError))
        {
            Debug.LogError(mError);
            SetBottomMsg("下载资源文件失败");
            return true;
        }
        return false;
    }

    void OnGetRemoteFileInfo(string fileName, string error, long fileSize, DateTime lastModified)
    {
        mFileName = fileName;
        mError = error;
        mFileSize = fileSize;
        mLastModified = lastModified;
    }

    void OnDownloadFileFinidhed(string fileName, string error)
    {
        mFileName = fileName;
        mError = error;
    }
}
