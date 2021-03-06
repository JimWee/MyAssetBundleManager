﻿using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AssetBundles
{
    public delegate void OnLoadAssetFinished(UnityEngine.Object asset);

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
        public static bool TestUsedResourcesLoad = false;
        public static bool TestResourcesLoad = false;
        public static bool TestUsedLoad = false;
        public static bool TestPreLoad = false;
        public static bool TestUnload = true;
        public static AssetBundleLoader Instance = null;
        public static AsyncOperation LoadSceneAsyncOpe = null;
        AssetBundleManifest mAssetBundleManifest = null;
        Dictionary<string, LoadedAssetBundle> mLoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        Dictionary<string, AssetBundleInfo> mAssetBundleInfos = new Dictionary<string, AssetBundleInfo>();

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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
#if UNITY_EDITOR
            if (AssetBundleUtility.SimulateAssetBundleInEditor)
            {
                yield break;
            }
#endif
            if (mAssetBundleManifest != null)
            {
                Resources.UnloadAsset(mAssetBundleManifest);
                mAssetBundleManifest = null;
            }
            LoadSceneAsyncOpe = null;
            mAssetBundleInfos = null;
            UnloadAllAssetBundles(true);

            //解析version文件
            string error = "";
            Int64 versionID;
            byte[] versionBytes = File.ReadAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, AssetBundleUtility.VersionFileName));
            if (!AssetBundleUtility.ResolveEncryptedVersionData(versionBytes, ref mAssetBundleInfos, out versionID, out error))
            {
                Debug.LogErrorFormat("resolve version file failed: {0}", error);
                yield break;
            }

            //加载Manifest
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(AssetBundleName2FilePath(AssetBundleUtility.GetPlatformName() + AssetBundleUtility.AssetBundleExtension));
            yield return bundleRequest;
            AssetBundleRequest assetRequest = bundleRequest.assetBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest");
            yield return assetRequest;
            mAssetBundleManifest = assetRequest.asset as AssetBundleManifest;
            bundleRequest.assetBundle.Unload(false);
        }

        public UnityEngine.Object LoadAsset(string assetPath)
        {
#if UNITY_EDITOR
            if (AssetBundleUtility.SimulateAssetBundleInEditor)
            {
                return AssetDatabase.LoadMainAssetAtPath(Path.Combine(AssetBundleUtility.AssetBundleResourcesPath, assetPath) + ".prefab");
            }
#endif
            AssetBundle bundle = LoadAssetBundleWithDependencies(assetPath + AssetBundleUtility.AssetBundleExtension);
            string assetName = Path.GetFileName(assetPath);
            return bundle.LoadAsset(assetName);
        }

        public IEnumerator LoadAssetAsync(string assetPath, OnLoadAssetFinished onLoadAssetFinished)
        {
#if UNITY_EDITOR
            if (AssetBundleUtility.SimulateAssetBundleInEditor)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(Path.Combine(AssetBundleUtility.AssetBundleResourcesPath, assetPath) + ".prefab");
                onLoadAssetFinished(asset);
                yield break;
            }
#endif
            string assetBundleName = assetPath + AssetBundleUtility.AssetBundleExtension;
            yield return StartCoroutine(LoadAssetBundleWithDependenciesAsync(assetBundleName));
            AssetBundle bundle = mLoadedAssetBundles[assetBundleName].mAssetBundle;
            string assetName = Path.GetFileName(assetPath);
            AssetBundleRequest assetRequest = bundle.LoadAssetAsync(assetName);
            yield return assetRequest;
            if (assetRequest.asset == null)
            {
                Debug.LogErrorFormat("Load Asset Failed: {0}", assetName);
                yield break;
            }
            bundle.Unload(false);
            onLoadAssetFinished(assetRequest.asset);
        }

        /// <summary>
        /// 场景名字大小写敏感，坑~
        /// </summary>
        /// <param name="scenePath"></param>
        /// <param name="mode"></param>
        public void LoadScene(string scenePath, LoadSceneMode mode = LoadSceneMode.Single)
        {
#if UNITY_EDITOR
            if (AssetBundleUtility.SimulateAssetBundleInEditor)
            {
                string path = Path.Combine(AssetBundleUtility.AssetBundleResourcesPath, scenePath) + ".unity";
                if (mode == LoadSceneMode.Single)
                {
                    EditorApplication.LoadLevelInPlayMode(path);
                    
                }
                else
                {
                    EditorApplication.LoadLevelAdditiveInPlayMode(path);
                }
                return;
            }
#endif
            LoadAssetBundleWithDependencies(scenePath + AssetBundleUtility.AssetBundleExtension);
            SceneManager.LoadScene(Path.GetFileName(scenePath), mode);
        }

        public IEnumerator LoadSceneAsync(string scenePath, LoadSceneMode mode = LoadSceneMode.Single)
        {
#if UNITY_EDITOR
            if (AssetBundleUtility.SimulateAssetBundleInEditor)
            {
                string path = Path.Combine(AssetBundleUtility.AssetBundleResourcesPath, scenePath) + ".unity";
                if (mode == LoadSceneMode.Single)
                {
                    yield return EditorApplication.LoadLevelAsyncInPlayMode(path);

                }
                else
                {
                    yield return EditorApplication.LoadLevelAdditiveAsyncInPlayMode(path);
                }
                yield break;
            }
#endif

            yield return StartCoroutine(LoadAssetBundleWithDependenciesAsync(scenePath + AssetBundleUtility.AssetBundleExtension));
            LoadSceneAsyncOpe = SceneManager.LoadSceneAsync(Path.GetFileName(scenePath), mode);
            yield return LoadSceneAsyncOpe;
            LoadSceneAsyncOpe = null;
        }

        public AssetBundle LoadAssetBundleWithDependencies(string assetBundleName)
        {
            assetBundleName = assetBundleName.ToLower();

            //加载依赖AssetBundle
            string[] dependencies = mAssetBundleManifest.GetAllDependencies(assetBundleName);
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
            if (!mLoadedAssetBundles.TryGetValue(assetBundleName, out loadedAssetBundle))
            {
                LoadAssetBundle(assetBundleName);
            }
            else
            {
                loadedAssetBundle.mReferencedCount++;
            }

            return mLoadedAssetBundles[assetBundleName].mAssetBundle;
        }

        public void LoadAssetBundle(string assetBundleName)
        {
            assetBundleName = assetBundleName.ToLower();

            AssetBundle asssetBundle = AssetBundle.LoadFromFile(AssetBundleName2FilePath(assetBundleName));
            if (asssetBundle == null)
            {
                Debug.LogErrorFormat("Load AssetBundle Failed: {0}", assetBundleName);
                return;
            }
            LoadedAssetBundle loadedAssetBundle = new LoadedAssetBundle(asssetBundle);
            mLoadedAssetBundles.Add(assetBundleName, loadedAssetBundle);
        }

        public IEnumerator LoadAssetBundleWithDependenciesAsync(string assetBundleName)
        {
            assetBundleName = assetBundleName.ToLower();

            //加载依赖AssetBundle
            string[] dependencies = mAssetBundleManifest.GetAllDependencies(assetBundleName);
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
            if (!mLoadedAssetBundles.TryGetValue(assetBundleName, out loadedAssetBundle))
            {
                yield return StartCoroutine(LoadAssetBundleAsync(assetBundleName));
            }
            else
            {
                loadedAssetBundle.mReferencedCount++;
            }
        }

        public IEnumerator LoadAssetBundleAsync(string assetBundleName)
        {
            assetBundleName = assetBundleName.ToLower();

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

        public void UnloadScene(string scenePath)
        {
            UnloadAsset(scenePath);
        }

        public void UnloadAsset(string assetPath)
        {
            assetPath = assetPath.ToLower() + AssetBundleUtility.AssetBundleExtension;

            UnloadAssetBundle(assetPath);

            foreach (var item in mAssetBundleManifest.GetAllDependencies(assetPath))
            {
                UnloadAssetBundle(item);
            }
        }

        public void UnloadAssetBundle(string assetBundleName)
        {
            assetBundleName = assetBundleName.ToLower();

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

        public void UnloadAllAssetBundles(bool unloadAllLoadedObjects)
        {
            foreach (var item in mLoadedAssetBundles)
            {
                item.Value.mAssetBundle.Unload(unloadAllLoadedObjects);
            }
            mLoadedAssetBundles.Clear();
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }
    }
}

