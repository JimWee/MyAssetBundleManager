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
        //public static AssetBundleManifest LocalAssetBundleManifest = null;
        //public static AssetBundleManifest ServerAssetBundleManifest = null;
        public static Dictionary<string, AssetBundleInfo> LocalAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> ServerAssetBundleInfos = null;
        public static Dictionary<string, AssetBundleInfo> DowloadAssetBundleInfos = null;

        private static Dictionary<string, string> mDownloadingErrors = new Dictionary<string, string>();
        private static Dictionary<string, WWW> mDoneWWWs = new Dictionary<string, WWW>();

        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath + AssetBundleUtility.GetPlatformName() + "/";
        }

        //public static IEnumerator LoadLocalAssetBundleManifestAsync()
        //{
        //    AssetBundleCreateRequest loadAssetBundleRequest = AssetBundle.LoadFromFileAsync(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.GetPlatformName()));
        //    yield return loadAssetBundleRequest;
        //    AssetBundle loadedAssetBundle = loadAssetBundleRequest.assetBundle;
        //    AssetBundleRequest loadAssetRequest = loadedAssetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
        //    yield return loadAssetRequest;
        //    LocalAssetBundleManifest = loadAssetRequest.asset as AssetBundleManifest;
        //}

        //public static IEnumerator LoadServerAssetBundleManifestAsync()
        //{
        //    using (WWW www = new WWW(BaseDownloadingURL + AssetBundleUtility.GetPlatformName()))
        //    {
        //        yield return www;

        //        if (!String.IsNullOrEmpty(www.error))
        //        {
        //            Debug.LogWarning(www.error);
        //            yield break;
        //        }
        //    } 
        //}

        public static void SetDownloadAssetBundleInfos()
        {
            DowloadAssetBundleInfos = new Dictionary<string, AssetBundleInfo>();

            if (ServerAssetBundleInfos == null)
            {
                return;
            }

            if (LocalAssetBundleInfos == null)
            {
                DowloadAssetBundleInfos = new Dictionary<string, AssetBundleInfo>(ServerAssetBundleInfos);
                return;
            }
        
            foreach (var item in ServerAssetBundleInfos)
            {
                AssetBundleInfo assetBundleInfo;
                if (LocalAssetBundleInfos.TryGetValue(item.Key, out assetBundleInfo))
                {
                    if (!item.Value.MD5.Equals(assetBundleInfo.MD5))
                    {
                        DowloadAssetBundleInfos.Add(item.Key, item.Value);
                    }
                }
                else
                {
                    DowloadAssetBundleInfos.Add(item.Key, item.Value);
                }
            }
            return;
        }

        public static void FilterDownloadAssetBundleInfos()
        {
            List<string> itemToRemove = new List<string>();
            if (DowloadAssetBundleInfos != null)
            {
                foreach (var item in DowloadAssetBundleInfos)
                {
                    string path = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, item.Value.AssetBundleName);
                    if (File.Exists(path))
                    {
                        string MD5 = AssetBundleUtility.GetMD5HashFromFile(path);
                        if (MD5.Equals(item.Value.MD5))
                        {
                            itemToRemove.Add(item.Key);
                        }
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
                    File.WriteAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, fileName), www.bytes);
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

    }
}
