using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AssetBundles
{
    public struct AssetBundleInfo
    {
        public string AssetBundleName;
        public string MD5;
        public int Size;
    }

    public class AssetBundleUpdate
    {
        
        public static string BaseDownloadingURL = "";
        public static Dictionary<string, AssetBundleInfo> LocalAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> ServerAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> DowloadAssetBundleInfos = null;

        private static Dictionary<string, string> mDownloadingErrors = new Dictionary<string, string>();
        private static Dictionary<string, WWW> mDoneWWWs = new Dictionary<string, WWW>();

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

        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath + AssetBundleUtility.GetPlatformName() + "/";
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

        public static IEnumerator DownloadFile(string fileName)
        {
            WWW www = new WWW(BaseDownloadingURL + fileName);
            yield return www;
            if (www.error != null)
            {
                mDownloadingErrors.Add(fileName, string.Format("Failed downloading file {0} from {1}: {2}", fileName, www.url, www.error));
                yield break;
            }

            if (www.isDone)
            {
                mDoneWWWs.Add(fileName, www);
            }
        }

        public static bool SaveFile(string fileName, out string error)
        {
            WWW www;
            if (mDoneWWWs.TryGetValue(fileName, out www))
            {
                try
                {
                    string filePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, fileName);
                    string dir = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllBytes(filePath, www.bytes);
                    error = null;
                    return true;
                }
                catch (Exception ex)
                {

                    error = ex.ToString();
                    return false;
                }                
            }
            else
            {
                error = string.Format("file www don't exist: {0}", fileName);
                return false;
            }
        }

        public static bool GetDoneWWW(string fileName, out WWW www)
        {
            return mDoneWWWs.TryGetValue(fileName, out www);
        }

        public static bool RemoveDoneWWW(string fileName)
        {
            WWW www;
            if (mDoneWWWs.TryGetValue(fileName, out www))
            {
                www.Dispose();
                mDoneWWWs.Remove(fileName);
                return true;
            }
            return false;
        }

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
            mDownloadingErrors = null;
            mDoneWWWs = null;
        }

    }
}
