using UnityEngine;
using System;
using System.Collections;
using System.IO;

namespace AssetBundles
{
    public class AssetBundleDownloader
    {
        public static string BaseDownloadingURL = "";
        public static AssetBundleManifest LocalAssetBundleManifest = null;
        public static AssetBundleManifest ServerAssetBundleManifest = null;

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
    }
}
