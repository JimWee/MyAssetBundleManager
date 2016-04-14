using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AssetBundles
{
    public delegate void OnLoadAssetFinished(Object asset);

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

        public static AssetBundleLoader Instance = null;
        AssetBundleManifest mAssetBundleManifest = null;
        Dictionary<string, LoadedAssetBundle> mLoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        Dictionary<string, AssetBundleInfo> mAssetBundleInfos = new Dictionary<string, AssetBundleInfo>();

        void Awake()
        {
            Instance = this;
        }

        public string AssetBundleName2FilePath(string assetBundleName)
        {
            AssetBundleInfo info;
            if (mAssetBundleInfos.TryGetValue(assetBundleName, out info))
            {
                return Path.Combine(AssetBundleUtility.LocalAssetBundlePath, info.MD5);
            }
            return string.Empty; 
        }

        public IEnumerator Init()
        {
            //解析version文件
            string error = "";
            byte[] versionBytes = File.ReadAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName));
            if (!AssetBundleUtility.ResolveEncryptedVersionData(versionBytes, ref mAssetBundleInfos, out error))
            {
                Debug.LogErrorFormat("resolve version file failed: {0}", error);
                yield break;
            }

            //加载Manifest
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(AssetBundleName2FilePath(AssetBundleUtility.GetPlatformName()));
            yield return bundleRequest;
            AssetBundleRequest assetRequest = bundleRequest.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
            yield return assetRequest;
            mAssetBundleManifest = assetRequest.asset as AssetBundleManifest;
            bundleRequest.assetBundle.Unload(false);
        }

        public Object LoadAsset(string assetPath)
        {
            assetPath = assetPath.ToLower();

            //加载依赖AssetBundle
            string[] dependencies = mAssetBundleManifest.GetAllDependencies(assetPath);
            foreach (var item in dependencies)
            {
                LoadedAssetBundle loadedDependencyAssetBundle = null;
                if (!mLoadedAssetBundles.TryGetValue(item, out loadedDependencyAssetBundle))
                {
                    LoadAssetBundle(item);
                }
                else
                {
                    loadedDependencyAssetBundle.mReferencedCount++;

                }
            }

            LoadedAssetBundle loadedAssetBundle = null;
            if (!mLoadedAssetBundles.TryGetValue(assetPath, out loadedAssetBundle))
            {
                LoadAssetBundle(assetPath);
            }
            else
            {
                loadedAssetBundle.mReferencedCount++;
            }

            AssetBundle bundle = mLoadedAssetBundles[assetPath].mAssetBundle;
            string assetName = Path.GetFileName(assetPath);
            return bundle.LoadAsset(assetName);
        }

        public IEnumerator LoadAssetAsync(string assetPath, OnLoadAssetFinished onLoadAssetFinished)
        {
            assetPath = assetPath.ToLower();

            //加载依赖AssetBundle
            string[] dependencies = mAssetBundleManifest.GetAllDependencies(assetPath);
            foreach (var item in dependencies)
            {
                LoadedAssetBundle loadedDependencyAssetBundle = null;
                if (!mLoadedAssetBundles.TryGetValue(item, out loadedDependencyAssetBundle))
                {
                    yield return StartCoroutine(LoadAssetBundleAsync(item));
                }
                else
                {
                    loadedDependencyAssetBundle.mReferencedCount++;

                }
            }

            LoadedAssetBundle loadedAssetBundle = null;
            if (!mLoadedAssetBundles.TryGetValue(assetPath, out loadedAssetBundle))
            {
                yield return StartCoroutine(LoadAssetBundleAsync(assetPath));
            }
            else
            {
                loadedAssetBundle.mReferencedCount++;
            }

            AssetBundle bundle = mLoadedAssetBundles[assetPath].mAssetBundle;
            string assetName = Path.GetFileName(assetPath);
            AssetBundleRequest assetRequest = bundle.LoadAssetAsync(assetName);
            yield return assetRequest;
            if (assetRequest.asset == null)
            {
                Debug.LogErrorFormat("Load Asset Failed: {0}", assetName);
                yield break;
            }
            onLoadAssetFinished(assetRequest.asset);
        }

        public void LoadAssetBundle(string assetBundleName)
        {
            AssetBundle asssetBundle = AssetBundle.LoadFromFile(AssetBundleName2FilePath(assetBundleName));
            if (asssetBundle == null)
            {
                Debug.LogErrorFormat("Load AssetBundle Failed: {0}", assetBundleName);
                return;
            }
            LoadedAssetBundle loadedAssetBundle = new LoadedAssetBundle(asssetBundle);
            mLoadedAssetBundles.Add(assetBundleName, loadedAssetBundle);
        }

        public IEnumerator LoadAssetBundleAsync(string assetBundleName)
        {
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(AssetBundleName2FilePath(assetBundleName));
            yield return bundleRequest;
            if (bundleRequest.assetBundle == null)
            {
                Debug.LogErrorFormat("Load AssetBundle Failed: {0}", assetBundleName);
                yield break;
            }
            LoadedAssetBundle loadedAssetBundle = new LoadedAssetBundle(bundleRequest.assetBundle);
            mLoadedAssetBundles.Add(assetBundleName, loadedAssetBundle);
        }

        public void UnloadAsset(string assetPath)
        {
            assetPath = assetPath.ToLower();
            UnloadAssetBundle(assetPath);

            foreach (var item in mAssetBundleManifest.GetAllDependencies(assetPath))
            {
                UnloadAssetBundle(item);
            }
        }

        public void UnloadAssetBundle(string assetBundleName)
        {
            LoadedAssetBundle loadedAssetBundle = null;
            if (mLoadedAssetBundles.TryGetValue(assetBundleName, out loadedAssetBundle))
            {
                loadedAssetBundle.mReferencedCount--;
                if (loadedAssetBundle.mReferencedCount == 0)
                {
                    loadedAssetBundle.mAssetBundle.Unload(false);
                    mLoadedAssetBundles.Remove(assetBundleName);
                }
            }
        }
    }
}

