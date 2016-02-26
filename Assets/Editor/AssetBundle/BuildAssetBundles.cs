using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AssetBundles
{
    public class BuildAssetBundles
    {
        static public string AssetBundleResourcesPath = "Assets/AssetBundleResources";

        [MenuItem("AssetBundles/Build AssetBundles")]
        static public void Build()
        {
            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            string[] lookFor = new string[] { AssetBundleResourcesPath };
            string[] guids = AssetDatabase.FindAssets("", lookFor);
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(assetPath))//排除文件夹
                {
                    AssetBundleBuild build = new AssetBundleBuild();
                    build.assetBundleName = AssetPathToAssetBundleName(assetPath);
                    build.assetNames = new string[] { assetPath };
                    builds.Add(build);
                    Debug.Log(assetPath);
                }
            }
            string outputPath = Path.Combine(AssetBundleUtility.AssetBundlesOutputPath, AssetBundleUtility.GetPlatformName());
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
        }

        static string AssetPathToAssetBundleName(string assetPath)
        {
            string assetbundleName = assetPath.Replace(AssetBundleResourcesPath + "/", "");
            return assetbundleName.Substring(0, assetbundleName.IndexOf('.'));
        }
    }
}
