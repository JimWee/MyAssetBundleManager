using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
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

    public delegate void OnGetRemoteFileInfo(string error, long fileSize, DateTime lastModified);
    public delegate void OnDownloadFileFinished(string error);

    public class AssetBundleUpdate
    {
        
        public static string BaseDownloadingURL = "";
        public static Dictionary<string, AssetBundleInfo> LocalAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> ServerAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> DowloadAssetBundleInfos = null;
        public static WWW DownloadingWWW = null;

        private static Dictionary<string, string> mDownloadingErrors = new Dictionary<string, string>();
        private static Dictionary<string, WWW> mDoneWWWs = new Dictionary<string, WWW>();        

        /// <summary>
        /// 获取更新地址
        /// </summary>
        /// <returns></returns>
        public static string GetAssetBundleServerUrl()
        {
            TextAsset urlFile = Resources.Load("AssetBundleServerURL") as TextAsset;
            string url = (urlFile != null) ? urlFile.text.Trim() : null;
            if (url == null || url.Length == 0)
            {
                Debug.LogError("Server URL could not be found.");
            }
            return url;
        }

        /// <summary>
        /// 设置更新地址
        /// </summary>
        /// <param name="absolutePath"></param>
        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath + AssetBundleUtility.GetPlatformName() + "/";
        }

        /// <summary>
        /// 解析本地version文件
        /// </summary>
        /// <returns>
        /// -1 = 本地version文件不存在
        /// -2 = 解析version文件失败
        /// </returns>
        public static int ResolveLocalVersionFile()
        {
            string filePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName);
            if (!File.Exists(filePath)) return -1;
            string error = null;
            if (!AssetBundleUtility.ResolveEncryptedVersionData(File.ReadAllBytes(filePath), ref LocalAssetBundleInfos, out error))
            {
                Debug.LogError("解析本地版本文件失败：" + error);
                return -2;
            }
            return 0;
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
                    if (File.Exists(path))
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
        public static void GetDowloadInfo(out int count, out float size)
        {
            count = 0;
            size = 0;
            if (DowloadAssetBundleInfos != null)
            {
                count = DowloadAssetBundleInfos.Count;
                foreach (var item in DowloadAssetBundleInfos)
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
                    onGetRemoteFileInfo(error, 0, DateTime.Now);
                    yield break;
                }
                string strLength = request.GetResponseHeader("Content-Length");
                if (string.IsNullOrEmpty(strLength))
                {
                    onGetRemoteFileInfo("GetRemoteFileInfo - can not get Content-Length", 0, DateTime.Now);
                    yield break;
                }
                long fileSize = Convert.ToInt64(strLength);
                string strDateTime = request.GetResponseHeader("Last-Modified");
                if (string.IsNullOrEmpty(strDateTime))
                {
                    onGetRemoteFileInfo("GetRemoteFileInfo - can not get Last-Modified", 0, DateTime.Now);
                    yield break;
                }
                DateTime lastModified = DateTime.Parse(strDateTime);
                onGetRemoteFileInfo(null, fileSize, lastModified);
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
                        onDownloadFileFinidhed(null);
                        yield break;
                    }
                    if (localFileInfo.Length < remoteFileSize && !isOutdate)//继续下载
                    {
                        request.downloadHandler = new DownloadHandlerFile(filePath, FileMode.Append);
                        request.SetRequestHeader("Range", string.Format("bytes={0}-", localFileInfo.Length));
                    }
                }
            }
        }

        /// <summary>
        /// 文件是否过时
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="remoteFileSize"></param>
        /// <param name="remoteFileLastModified"></param>
        /// <returns>
        ///     1：过时
        ///     0：已下载完成
        ///     -1：需要继续下载
        /// </returns>
        private static int IsFileOutdate(string fileName, long remoteFileSize, DateTime remoteFileLastModified)
        {
            string filePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, fileName);
            if (!File.Exists(filePath))
            {
                return 1;
            }
            FileInfo localFileInfo = new FileInfo(filePath);
            if (remoteFileLastModified >= localFileInfo.LastWriteTime)
            {
                return 1;
            }
            if (localFileInfo.Length > remoteFileSize)
            {
                return 1;
            }
            if (localFileInfo.Length == remoteFileSize)
            {
                return 0;
            }
            return -1;

        }

        //public static IEnumerator DownloadFile(string fileName)
        //{
        //    WWW www = new WWW(BaseDownloadingURL + fileName);
        //    DownloadingWWW = www;
        //    yield return www;
        //    if (www.error != null)
        //    {
        //        mDownloadingErrors.Add(fileName, string.Format("Failed downloading file {0} from {1}: {2}", fileName, www.url, www.error));
        //        DownloadingWWW = null;
        //        yield break;
        //    }

        //    if (www.isDone)
        //    {                
        //        mDoneWWWs.Add(fileName, www);
        //        DownloadingWWW = null;
        //    }
        //}

        //public static bool SaveFile(string fileName, out string error)
        //{
        //    WWW www;
        //    if (mDoneWWWs.TryGetValue(fileName, out www))
        //    {
        //        try
        //        {
        //            string filePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, fileName);
        //            string dir = Path.GetDirectoryName(filePath);
        //            if (!Directory.Exists(dir))
        //            {
        //                Directory.CreateDirectory(dir);
        //            }
        //            File.WriteAllBytes(filePath, www.bytes);
        //            error = null;
        //            return true;
        //        }
        //        catch (Exception ex)
        //        {

        //            error = ex.ToString();
        //            return false;
        //        }                
        //    }
        //    else
        //    {
        //        error = string.Format("file www don't exist: {0}", fileName);
        //        return false;
        //    }
        //}

        //public static bool GetDoneWWW(string fileName, out WWW www)
        //{
        //    return mDoneWWWs.TryGetValue(fileName, out www);
        //}

        //public static bool RemoveDoneWWW(string fileName)
        //{
        //    WWW www;
        //    if (mDoneWWWs.TryGetValue(fileName, out www))
        //    {
        //        www.Dispose();
        //        mDoneWWWs.Remove(fileName);
        //        return true;
        //    }
        //    return false;
        //}

        public static bool GetDownloadingError(string fileName, out string error)
        {
            return mDownloadingErrors.TryGetValue(fileName, out error);
        }

        public static bool RemoveDownloadingError(string fileName)
        {
            return mDownloadingErrors.Remove(fileName);
        }

        public static void Clear()
        {
            LocalAssetBundleInfos = null;
            ServerAssetBundleInfos = null;
            DowloadAssetBundleInfos = null;
            mDownloadingErrors.Clear();
            mDoneWWWs.Clear();
        }

    }
}
