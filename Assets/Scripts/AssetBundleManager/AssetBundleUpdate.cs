using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine.Experimental.Networking;

namespace AssetBundles
{
    public struct AssetBundleInfo
    {
        public string AssetBundleName;
        public string MD5;
        public int Size;
    }

    public struct PatchesInfo
    {
        public Int64 From;
        public Int64 To;
        public string MD5;
        public int Size;
    }

    public delegate void OnGetRemoteFileInfo(string fileName, string error, long fileSize, DateTime lastModified);
    public delegate void OnDownloadFileFinished(string fileName, string error);

    public class AssetBundleUpdate
    {
        
        public static string BaseDownloadingURL = "";
        public static Dictionary<string, AssetBundleInfo> LocalAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> ServerAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> DowloadAssetBundleInfos = null;

        public static UnityWebRequest CurrentRequest = null;

        public static Int64 ResourcesVersionID;

        /// <summary>
        /// 设置更新地址
        /// </summary>
        /// <param name="absolutePath"></param>
        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath + AssetBundleUtility.GetPlatformName() + "/";
        }


        public static bool ResolvePatchesList(byte[] bytes, out List<PatchesInfo> patchesInfos, out string error)
        {
            patchesInfos = new List<PatchesInfo>();
            try
            {                
                string text = Encoding.UTF8.GetString(bytes);
                string[] items = text.Split('\n');
                foreach (var item in items)
                {
                    string[] infos = item.Split('\t');
                    PatchesInfo patchesInfo;
                    patchesInfo.From = Convert.ToInt64(infos[0]);
                    patchesInfo.To = Convert.ToInt64(infos[1]);
                    patchesInfo.MD5 = infos[2];
                    patchesInfo.Size = Convert.ToInt32(infos[3]);
                    patchesInfos.Add(patchesInfo);
                }
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = string.Format("Failed resolving patchesList: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// 比较本地和服务器version文件，确定需要下载的文件（新文件和MD5改变的文件）
        /// </summary>
        public static void SetDownloadAssetBundleInfos()
        {
            DowloadAssetBundleInfos = new Dictionary<string, AssetBundleInfo>();

            if (ServerAssetBundleInfos == null)
            {
                return;
            }

            if (LocalAssetBundleInfos == null)
            {            
                DowloadAssetBundleInfos = new Dictionary<string, AssetBundleInfo>(ServerAssetBundleInfos);//没有本地version文件，下载服务器version所有文件
                return;
            }
        
            foreach (var item in ServerAssetBundleInfos)
            {
                AssetBundleInfo assetBundleInfo;
                if (LocalAssetBundleInfos.TryGetValue(item.Key, out assetBundleInfo))//已有文件
                {
                    if (!item.Value.MD5.Equals(assetBundleInfo.MD5))//MD5不同，文件有改动
                    {
                        DowloadAssetBundleInfos.Add(item.Key, item.Value);
                    }
                }
                else//新文件
                {
                    DowloadAssetBundleInfos.Add(item.Key, item.Value);
                }
            }

            FilterDownloadAssetBundleInfos();

            return;
        }

        /// <summary>
        /// 过滤需下载文件，除去本地已有文件
        /// </summary>
        public static void FilterDownloadAssetBundleInfos()
        {
            List<string> itemToRemove = new List<string>();
            if (DowloadAssetBundleInfos != null)
            {
                foreach (var item in DowloadAssetBundleInfos)
                {
                    string path = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, item.Value.MD5);
                    if (File.Exists(path) && AssetBundleUtility.GetMD5HashFromFile(path) == item.Value.MD5)
                    {
                        itemToRemove.Add(item.Key);
                    }
                }
            }
            foreach (var item in itemToRemove)
            {
                DowloadAssetBundleInfos.Remove(item);
            }
        }

        /// <summary>
        /// 获取下载文件数量和大小
        /// </summary>
        /// <param name="count"></param>
        /// <param name="size"></param>
        public static void GetDowloadInfo(Dictionary<string, AssetBundleInfo> assetBundleInfos, out int count, out float size)
        {
            count = 0;
            size = 0;
            if (assetBundleInfos != null)
            {
                count = assetBundleInfos.Count;
                foreach (var item in assetBundleInfos)
                {
                    size += item.Value.Size;
                }
            }
            return;
        }

        /// <summary>
        /// 获取下载文件的大小和最后修改时间
        /// </summary>
        /// <param name="url"></param>
        /// <param name="onGetRemoteFileInfo"></param>
        /// <returns></returns>
        public static IEnumerator GetRemoteFileInfo(string fileName, OnGetRemoteFileInfo onGetRemoteFileInfo)
        {
            string url = BaseDownloadingURL + fileName;
            using (UnityWebRequest request = UnityWebRequest.Head(url))
            {
                yield return request.Send();
                if (request.isError)
                {
                    string error = string.Format("GetRemoteFileInfo - url: {0}, responseCode: {1}, error: {2}",
                                                    url, request.responseCode, request.error);
                    onGetRemoteFileInfo(fileName, error, 0, DateTime.Now);
                    yield break;
                }
                string strLength = request.GetResponseHeader("Content-Length");
                if (string.IsNullOrEmpty(strLength))
                {
                    onGetRemoteFileInfo(fileName, "GetRemoteFileInfo - can not get Content-Length", 0, DateTime.Now);
                    yield break;
                }
                long fileSize = Convert.ToInt64(strLength);
                string strDateTime = request.GetResponseHeader("Last-Modified");
                if (string.IsNullOrEmpty(strDateTime))
                {
                    onGetRemoteFileInfo(fileName, "GetRemoteFileInfo - can not get Last-Modified", 0, DateTime.Now);
                    yield break;
                }
                DateTime lastModified = DateTime.Parse(strDateTime);
                onGetRemoteFileInfo(fileName, null, fileSize, lastModified);
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileMode">Append为断点续传，Create为新建文件</param>
        /// <param name="remoteFileSize">Append模式下必传，远端文件大小</param>
        /// <param name="remoteLastModified">Append模式下必传，远端文件最后修改日期</param>
        /// <returns></returns>
        public static IEnumerator DownloadFile(string fileName, OnDownloadFileFinished onDownloadFileFinidhed,
            FileMode fileMode = FileMode.Create, long remoteFileSize = 0, DateTime remoteLastModified = new DateTime())
        {
            string url = BaseDownloadingURL + fileName;
            string filePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, fileName);
            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                if (File.Exists(filePath) && fileMode == FileMode.Append)
                {
                    FileInfo localFileInfo = new FileInfo(filePath);
                    bool isOutdate = remoteLastModified > localFileInfo.LastWriteTime;
                    if(localFileInfo.Length == remoteFileSize && !isOutdate)//已下载完成
                    {
                        onDownloadFileFinidhed(fileName, null);
                        yield break;
                    }
                    if (localFileInfo.Length < remoteFileSize && !isOutdate)//继续下载
                    {
                        request.downloadHandler = new DownloadHandlerFile(filePath, FileMode.Append);
                        request.SetRequestHeader("Range", string.Format("bytes={0}-", localFileInfo.Length));
                    }
                    else//重新下载
                    {
                        request.downloadHandler = new DownloadHandlerFile(filePath);
                    }
                }
                else
                {
                    request.downloadHandler = new DownloadHandlerFile(filePath);
                }
                CurrentRequest = request;
                yield return request.Send();
                string error = request.isError ?
                    string.Format("DownloadFile Failed - url: {0}, responseCode: {1}, error: {2}", url, request.responseCode, request.error)
                    : null;
                onDownloadFileFinidhed(fileName, error);
            }
            CurrentRequest = null;
        }

        public static float GetDownloadSpeed()
        {
            if (CurrentRequest != null)
            {
                return ((DownloadHandlerFile)CurrentRequest.downloadHandler).GetDownloadSpeed();
            }
            return 0;
        }

        public static float GetDownloadPorgress()
        {
            if (CurrentRequest != null)
            {
                return CurrentRequest.downloadProgress;
            }
            return 0;
        }

        public static void Clear()
        {
            LocalAssetBundleInfos = null;
            ServerAssetBundleInfos = null;
            DowloadAssetBundleInfos = null;
        }

    }
}
