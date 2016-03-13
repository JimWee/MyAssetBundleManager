using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AssetBundles
{
    public class LoadedAssetBundle
    {
        public AssetBundle mAssetBundle;
        public int mReferencedCount;

        public LoadedAssetBundle(AssetBundle assetBundle)
        {
            mAssetBundle = assetBundle;
            mReferencedCount = 1;
        }
    }

    public class AssetBundleLoader : MonoBehaviour
    {

        public AssetBundleLoader Instance = null;
        public Object LoadedAsset = null;
        AssetBundleManifest mAssetBundleManifest = null;
        Dictionary<string, LoadedAssetBundle> mLoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();


        void Awake()
        {
            Instance = this;
        }

        public IEnumerator Init()
        {
            //加载Manifest
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.GetPlatformName()));
            yield return bundleRequest;
            AssetBundleRequest assetRequest = bundleRequest.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
            yield return assetRequest;
            mAssetBundleManifest = assetRequest.asset as AssetBundleManifest;
        }

        public IEnumerator LoadAssetAsync(string assetPath)
        {
            if (!mLoadedAssetBundles.ContainsKey(assetPath))
            {
                //加载依赖AssetBundle
                string[] dependencies = mAssetBundleManifest.GetDirectDependencies(assetPath);
                foreach (var item in dependencies)
                {
                    LoadedAssetBundle loadedAssetBundle = null;
                    if (!mLoadedAssetBundles.TryGetValue(item, out loadedAssetBundle))
                    {
                        yield return StartCoroutine(LoadAssetBundleAsync(item));
                    }
                    else
                    {
                        loadedAssetBundle.mReferencedCount++;

                    }
                }

                yield return StartCoroutine(LoadAssetBundleAsync(assetPath));
            }

            string assetName = Path.GetFileName(assetPath);
            AssetBundle bundle = mLoadedAssetBundles[assetPath].mAssetBundle;
            AssetBundleRequest assetRequest = bundle.LoadAssetAsync(assetName);
            yield return assetRequest;
            if (assetRequest.asset == null)
            {
                Debug.LogErrorFormat("Load Asset Failed: {0}", assetName);
                yield break;
            }
            LoadedAsset = assetRequest.asset;
        }

        public IEnumerator LoadAssetBundleAsync(string assetBundleName)
        {
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, assetBundleName));
            yield return bundleRequest;
            if (bundleRequest.assetBundle == null)
            {
                Debug.LogErrorFormat("Load AssetBundle Failed: {0}", assetBundleName);
                yield break;
            }
            LoadedAssetBundle loadedAssetBundle = new LoadedAssetBundle(bundleRequest.assetBundle);
            mLoadedAssetBundles.Add(assetBundleName, loadedAssetBundle);
        }
    }
}

