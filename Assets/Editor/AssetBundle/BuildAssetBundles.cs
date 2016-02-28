using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetBundles
{
    public class BuildAssetBundles
    {
        public const string AssetBundleResourcesPath = "Assets/AssetBundleResources";
        public const string AssetBundlesOutputPath = "AssetBundles";

        [MenuItem("AssetBundles/Build AssetBundles")]
        static public void Build()
        {
            string outputPath = Path.Combine(AssetBundlesOutputPath, AssetBundleUtility.GetPlatformName());
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            List<string> paths = new List<string>();
            paths.Add(AssetBundleUtility.GetPlatformName());

            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();

            string[] lookFor = new string[] { AssetBundleResourcesPath };           
            string[] guids = AssetDatabase.FindAssets("", lookFor);
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(assetPath))//排除文件夹
                {
                    string assetBundleName = AssetPathToAssetBundleName(assetPath).ToLower();
                    paths.Add(assetBundleName);

                    AssetBundleBuild build = new AssetBundleBuild();
                    build.assetBundleName = assetBundleName;
                    build.assetNames = new string[] { assetPath };
                    builds.Add(build);
                    //Debug.Log(build.assetBundleName);
                    //Debug.Log(assetPath);
                }
            }

            BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);

            StringBuilder sb = new StringBuilder();
            foreach (var path in paths)
            {
                FileStream fs = new FileStream(Path.Combine(outputPath, path), FileMode.Open);
                sb.AppendFormat("{0}\t{1}\t{2}\n", path, AssetBundleUtility.GetMD5HashFromFileStream(fs), fs.Length);
                fs.Close();
            }
            File.WriteAllBytes(Path.Combine(outputPath, AssetBundleUtility.VersionFileName), Encoding.UTF8.GetBytes(sb.ToString()));
        }

        static string AssetPathToAssetBundleName(string assetPath)
        {     
            string assetbundleName = assetPath.Replace(AssetBundleResourcesPath + "/", "");
            return assetbundleName.Substring(0, assetbundleName.LastIndexOf('.'));
        }
    }
}
