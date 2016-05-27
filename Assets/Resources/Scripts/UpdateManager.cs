using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AssetBundles;
using System;
using UnityEngine.Experimental.Networking;

public class UpdateManager : MonoBehaviour
{
    public string URL;
    public GameObject UIPopMsg;
    public GameObject UIProgressBar;
    public GameObject UIBottomMsg;
    public GameObject UIResourcesVersion;
    public GameObject UIStartGameBtn;

    Text mBottomMsgTextCpt;
    Text mProgressBarTextCpt;
    Slider mProgressBarSliderCpt;

    string mBottomMsgFormatString = string.Empty;

    GameObject mCube;
    string mFileName = string.Empty;
    string mError = string.Empty;
    long mFileSize = 0;
    DateTime mLastModified;
    bool mIsDownloading = false;
    bool mIsDecompressing = false;
    int mZipFileNumber = 0;
    int[] mDecompressProgress = new int[1];
    bool mIsLoadingScene = false;

    delegate void ConfirmDelegate(bool isOk);

    /// <summary>
    /// 检查LocalAssetBundlePath路径下是否有VersionFileName文件
    ///     有=>下载patchList，更新补丁包
    ///     无=>检查streamingAssetsPath路径下是否有ZipFileName
    ///         有=>解包到LocalAssetBundlePath，检查资源更新
    ///         无=>下载ZipFileName解包，检查资源更新
    /// </summary>
    /// <returns></returns>
    IEnumerator Start()
    {
        mBottomMsgTextCpt = UIBottomMsg.GetComponent<Text>();
        mProgressBarTextCpt = UIProgressBar.transform.Find("Text").GetComponent<Text>();
        mProgressBarSliderCpt = UIProgressBar.GetComponent<Slider>();
        UIStartGameBtn.SetActive(false);

#if UNITY_EDITOR
        if (AssetBundleUtility.SimulateAssetBundleInEditor)
        {
            yield return StartCoroutine(InitResources());
            UIStartGameBtn.SetActive(true);
            yield break;
        }
#endif

        AssetBundleUpdate.Init(URL);

        string localVersionFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName);

        //检查本地是否存在version文件
        if (!File.Exists(localVersionFilePath))//没有本地版本文件信息
        {
            bool downloadResourcesPack = false;
#if UNITY_ANDROID
            string zipFilePath = Path.Combine("assets/", AssetBundleUtility.ZipFileName);
            if (lzip.getEntrySize(Application.dataPath, zipFilePath) <= 0)
            {            
#else
            string zipFilePath = Path.Combine(Application.streamingAssetsPath, AssetBundleUtility.ZipFileName);
            if (!File.Exists(zipFilePath))
            {
#endif
                downloadResourcesPack = true;                
                yield return StartCoroutine(AssetBundleUpdate.GetRemoteFileInfo(AssetBundleUtility.ZipFileName, OnGetRemoteFileInfo));
                if (CheckError()) yield break;

                SetBottomMsg("首次运行游戏，下载游戏资源包");

                bool waitForConfirm = true;
                bool continueDownload = false;
                DisplayDialog(string.Format("需要下载{0}大小的游戏资源包",
                    AssetBundleUpdate.GetSizeString(mFileSize)),
                    (bool isOk) => { waitForConfirm = false; continueDownload = isOk; });
                while (waitForConfirm)
                {
                    yield return null;
                }
                if (!continueDownload)
                {
                    yield break;
                }

                mIsDownloading = true;
                mBottomMsgFormatString = "下载游戏资源包中...    {0}/s";
                yield return StartCoroutine(AssetBundleUpdate.DownloadFile(AssetBundleUtility.ZipFileName, OnDownloadFileFinidhed, FileMode.Append, mFileSize, mLastModified));                
                if (CheckError()) yield break;
                UpdateProgress(1.0f);
                yield return null;
                mIsDownloading = false;

                zipFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.ZipFileName);             
            }

#if UNITY_ANDROID
            //将资源从apk包中解压到LocalAssetBundlePath
            if (!downloadResourcesPack)
            {
                SetBottomMsg("提取游戏资源包...");
                mZipFileNumber = 100;
                mDecompressProgress[0] = 0;
                mIsDecompressing = true;
                yield return null;
                int resAndroid = 0;
                Thread thAndroid = new Thread
                    (() =>
                    {
                        resAndroid = lzip.extract_entry(Application.dataPath, zipFilePath, Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.ZipFileName));
                        mIsDecompressing = false;
                    });
                thAndroid.Start();
                int timeCount = 0;
                while (mIsDecompressing)
                {
                    mDecompressProgress[0] = Mathf.Min(timeCount++, mZipFileNumber);
                    yield return new WaitForSeconds(0.5f);
                }
                if (resAndroid < 0)
                {
                    SetBottomMsg(string.Format("提取资源失败：{0}", resAndroid));
                    yield break;
                }
                UpdateProgress(1.0f);
                yield return null;

                downloadResourcesPack = true;
                zipFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.ZipFileName);
            }
#endif

            SetBottomMsg("解压游戏资源包...");
            mZipFileNumber = lzip.getTotalFiles(zipFilePath);
            mDecompressProgress[0] = 0;
            mIsDecompressing = true;
            yield return null;
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
                yield return null;
            }
            if (res < 0)
            {
                SetBottomMsg(string.Format("解压资源失败：{0}", res));
                yield break;
            }
            UpdateProgress(1.0f);
            yield return null;

            if (downloadResourcesPack)
            {
                File.Delete(zipFilePath);
            }
        }

        //解析本地文件获取版本号
        if (!AssetBundleUtility.ResolveEncryptedVersionData(File.ReadAllBytes(localVersionFilePath), 
            ref AssetBundleUpdate.LocalAssetBundleInfos, out AssetBundleUpdate.ResourcesVersionID, out mError))
        {
            SetBottomMsg("本地资源解析失败");
            yield break;
        }

        SetResourcesVersion(AssetBundleUpdate.ResourcesVersionID);

        //下载patchesList确定更新文件
        SetBottomMsg("检查更新");
        WWW patchesListRequest = new WWW(AssetBundleUpdate.BaseDownloadingURL + AssetBundleUtility.PatchListFileName);
        yield return patchesListRequest;
        if (patchesListRequest.error != null)
        {
            SetBottomMsg("下载补丁列表失败");
            Debug.LogErrorFormat("PatchesList file download failed - url: {0}, error: {1}", patchesListRequest.url, patchesListRequest.error);
            yield break;
        }

        List<PatchesInfo> patchesInfos = null;
        if (!AssetBundleUpdate.ResolvePatchesList(patchesListRequest.bytes, out patchesInfos, out mError))
        {
            SetBottomMsg("解析补丁列表失败");
            Debug.LogError(mError);
            yield break;
        }

        //下载解压补丁文件
        int patchesIndex = 0;
        for(; patchesIndex < patchesInfos.Count; patchesIndex++)
        {
            if (AssetBundleUpdate.ResourcesVersionID < patchesInfos[patchesIndex].To)
            {
                break;
            }
        }

        //计算下载文件数量和大小
        int patchesCount = 0;
        int patchesSize = 0;
        for (int i = patchesIndex; i < patchesInfos.Count; i++)
        {
            patchesCount++;
            patchesSize += patchesInfos[i].Size;
        }

        if (patchesCount > 0)
        {
            //确认下载
            bool waitForConfirm = true;
            bool continueDownload = false;
            DisplayDialog(string.Format("更新文件数量 {0}个，更新文件大小 {1}", 
                patchesCount, AssetBundleUpdate.GetSizeString(patchesSize)), 
                (bool isOk) => { waitForConfirm = false; continueDownload = isOk; });
            while (waitForConfirm)
            {
                yield return null;
            }
            if (!continueDownload)
            {
                yield break;
            }
        }

        int currentPatchesCount = 0;
        for (; patchesIndex < patchesInfos.Count; patchesIndex++)
        {
            mBottomMsgFormatString =  string.Format("下载文件中({0}/{1})...  ", ++currentPatchesCount, patchesCount) + "{0}/s";
            PatchesInfo patchesInfo = patchesInfos[patchesIndex];
            string fileName = string.Format("{0}-{1}.zip", patchesInfo.From, patchesInfo.To);
            mIsDownloading = true;
            yield return null;
            yield return AssetBundleUpdate.DownloadFile(fileName, OnDownloadFileFinidhed);
            if (CheckError()) yield break;
            UpdateProgress(1.0f);
            yield return null;
            mIsDownloading = false;

            SetBottomMsg("解压文件...");
            string zipFilePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, fileName);
            mZipFileNumber = lzip.getTotalFiles(zipFilePath);
            mDecompressProgress[0] = 0;
            mIsDecompressing = true;            
            yield return null;

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
                yield return null;
            }
            if (res < 0)
            {
                SetBottomMsg(string.Format("解压资源失败：{0}", res));
                yield break;
            }
            UpdateProgress(1.0f);
            yield return null;
            File.Delete(zipFilePath);
        }

        //检查资源完整性
        SetBottomMsg("检查资源完整性");
        Dictionary<string, AssetBundleInfo> errorAssetBundleInfos;
        if (!AssetBundleUtility.CheckLocalAssetBundles(true, out errorAssetBundleInfos))
        {
            SetBottomMsg("资源不完整");
            AssetBundleUtility.PrintAssetBundleInfos(errorAssetBundleInfos);
            yield break;
        }

        yield return StartCoroutine(InitResources());
        UIStartGameBtn.SetActive(true);
    }

    IEnumerator InitResources()
    {
        //加载AssetBundleLoader
        SetBottomMsg("初始化资源");
        if (AssetBundleLoader.Instance == null)
        {
            GameObject abLoaderObj = new GameObject("AssetBundleLoader", typeof(AssetBundleLoader));
            DontDestroyOnLoad(abLoaderObj);
        }
        yield return StartCoroutine(AssetBundleLoader.Instance.Init());
        SetBottomMsg("初始化资源完成");       
    }

    // Update is called once per frame
    void Update()
    {
        if (mIsDownloading)
        {
            UpdateProgress(AssetBundleUpdate.GetDownloadPorgress());
            if (AssetBundleUpdate.CurrentRequest != null)
            {
                SetBottomMsgFormat(AssetBundleUpdate.GetSizeString(((DownloadHandlerFile)AssetBundleUpdate.CurrentRequest.downloadHandler).GetDownloadSpeed()));
            }            
        }
        else if (mIsDecompressing)
        {
            UpdateProgress(mDecompressProgress[0] / (float)mZipFileNumber);
        }
        else if (mIsLoadingScene)
        {
            if (AssetBundleLoader.LoadSceneAsyncOpe != null)
            {
                UpdateProgress(AssetBundleLoader.LoadSceneAsyncOpe.progress);
                mIsLoadingScene = !AssetBundleLoader.LoadSceneAsyncOpe.isDone;
            }
        }
        else
        {
            UIProgressBar.SetActive(false);
        }        
    }

    void SetBottomMsg(string text)
    {
        Debug.Log(text);
        mBottomMsgTextCpt.text = text;
    }

    void SetBottomMsgFormat(params object[] args)
    {
        mBottomMsgTextCpt.text = string.Format(mBottomMsgFormatString, args);
    }

    void SetResourcesVersion(Int64 version)
    {
        Text textCpt = UIResourcesVersion.GetComponent<Text>();
        textCpt.text = string.Format("资源版本：{0}", version);
    }

    void UpdateProgress(float value)
    {
        UIProgressBar.SetActive(true);
        mProgressBarTextCpt.text = string.Format("{0:F}%", value * 100);
        mProgressBarSliderCpt.value = value;
    }

    void DisplayDialog(string content, ConfirmDelegate func)
    {
        UIPopMsg.SetActive(true);
        UIPopMsg.transform.Find("Text").GetComponent<Text>().text = content;
        UIPopMsg.transform.Find("OkBtn").GetComponent<Button>().onClick.AddListener(() => { UIPopMsg.SetActive(false); func(true); });
        UIPopMsg.transform.Find("CancelBtn").GetComponent<Button>().onClick.AddListener(() => { UIPopMsg.SetActive(false); func(false); });
    }

    public void LoadCubeAsync()
    {
        StartCoroutine(AssetBundleLoader.Instance.LoadAssetAsync("prefabs/mycube", delegate (UnityEngine.Object asset) { mCube = Instantiate(asset) as GameObject; }));
    }

    public void LoadCube()
    {
        mCube = Instantiate(AssetBundleLoader.Instance.LoadAsset("prefabs/mycube")) as GameObject;
    }

    public void DestroyCube()
    {
        Destroy(mCube);
        Resources.UnloadUnusedAssets();
    }

    public void UnloadCube()
    {
        AssetBundleLoader.Instance.UnloadAsset("prefabs/mycube");
    }

    public void LoadScene()
    {
        AssetBundleLoader.Instance.LoadScene("Scenes/Scene2");
    }

    public void LoadSceneAsync()
    {
        StartCoroutine(AssetBundleLoader.Instance.LoadSceneAsync("Scenes/Scene2"));
    }

    public void StartGame()
    {
        StartCoroutine(AssetBundleLoader.Instance.LoadSceneAsync("Scenes/Level 01"));
        SetBottomMsg("加载场景中...");
        UpdateProgress(0.0f);
        mIsLoadingScene = true;
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
