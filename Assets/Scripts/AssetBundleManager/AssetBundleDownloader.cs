using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AssetBundles
{
    public class AssetBundleDownloader
    {
        public static string BaseDownloadingURL = "";
        public static AssetBundleManifest LocalAssetBundleManifest = null;
        public static AssetBundleManifest ServerAssetBundleManifest = null;

        private static Dictionary<string, string> mDownloadingErrors = new Dictionary<string, string>();
        private static Dictionary<string, WWW> mDoneWWWs = new Dictionary<string, WWW>();

        public static void SetSourceAssetBundleURL(string absolutePath)
        {
            BaseDownloadingURL = absolutePath + AssetBundleUtility.GetPlatformName() + "/";
        }

        public static IEnumerator LoadLocalAssetBundleManifestAsync()
        {
            AssetBundleCreateRequest loadAssetBundleRequest = AssetBundle.LoadFromFileAsync(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.GetPlatformName()));
            yield return loadAssetBundleRequest;
            AssetBundle loadedAssetBundle = loadAssetBundleRequest.assetBundle;
            AssetBundleRequest loadAssetRequest = loadedAssetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
            yield return loadAssetRequest;
            LocalAssetBundleManifest = loadAssetRequest.asset as AssetBundleManifest;
        }

        public static IEnumerator LoadServerAssetBundleManifestAsync()
        {
            using (WWW www = new WWW(BaseDownloadingURL + AssetBundleUtility.GetPlatformName()))
            {
                yield return www;

                if (!String.IsNullOrEmpty(www.error))
                {
                    Debug.LogWarning(www.error);
                    yield break;
                }
            } 
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
                    www.Dispose();
                    mDoneWWWs.Remove(fileName);
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
    }
}
