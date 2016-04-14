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
            string outputPathRaw = Path.Combine(outputPath, AssetBundleUtility.GetPlatformName());
            if (!Directory.Exists(outputPathRaw))
                Directory.CreateDirectory(outputPathRaw);

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

            BuildPipeline.BuildAssetBundles(outputPathRaw, builds.ToArray(), BuildAssetBundleOptions.DisableWriteTypeTree | BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                FileStream fs = new FileStream(Path.Combine(outputPathRaw, path), FileMode.Open);
                sb.AppendFormat("{0}\t{1}\t{2}\n", path, AssetBundleUtility.GetMD5HashFromFileStream(fs), fs.Length);
                fs.Close();
                EditorUtility.DisplayProgressBar("Compute MD5", string.Format("{0}/{1}  {2}", i + 1, paths.Count, path), (i + 1) / (float)paths.Count);
            }            
            File.WriteAllBytes(Path.Combine(outputPathRaw, AssetBundleUtility.VersionFileName), Encoding.UTF8.GetBytes(sb.ToString()));
            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("Build AssetBundles", "Build Success!", "OK");
        }

        static string AssetPathToAssetBundleName(string assetPath)
        {     
            string assetbundleName = assetPath.Replace(AssetBundleResourcesPath + "/", "");
            return assetbundleName.Substring(0, assetbundleName.LastIndexOf('.'));            
        }

        [MenuItem("AssetBundles/Update Resources Files")]
        static public void UpdateResourcesFiles()
        {
            string outputPath = Path.Combine(AssetBundlesOutputPath, AssetBundleUtility.GetPlatformName());
            string outputPathRaw = Path.Combine(outputPath, AssetBundleUtility.GetPlatformName());

            //获取资源目录下现有文件列表
            DirectoryInfo dirInfo = new DirectoryInfo(outputPath);
            FileInfo[] fileInfos = dirInfo.GetFiles();
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
            foreach (var item in fileInfos)
            {
                files.Add(item.Name, item);
            }

            //获取最新AssetBundle文件信息
            string error = string.Empty;
            Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            byte[] bytes = File.ReadAllBytes(Path.Combine(outputPathRaw, AssetBundleUtility.VersionFileName));
            File.WriteAllBytes(Path.Combine(outputPath, AssetBundleUtility.VersionFileName), AssetBundleUtility.Encrypt(bytes, AssetBundleUtility.SecretKey));
            if (!AssetBundleUtility.ResolveDecryptedVersionData(bytes, ref assetBundleInfos, out error))
            {
                Debug.LogError("resolve version file failed: " + error);
                return;
            }

            int index = 0;
            foreach (var item in assetBundleInfos)
            {
                index++;
                if (files.ContainsKey(item.Value.MD5))//已有文件
                {
                    files.Remove(item.Key);
                }
                else//新文件
                {
                    File.Copy(Path.Combine(outputPathRaw, item.Key), Path.Combine(outputPath, item.Value.MD5), true);
                }
                EditorUtility.DisplayProgressBar("Copy New File", string.Format("{0}/{1}  {2}", index, assetBundleInfos.Count, item.Value.MD5), index / (float)assetBundleInfos.Count);
            }
            //删除旧文件
            index = 0;
            foreach (var item in files)
            {
                index++;
                File.Delete(item.Value.FullName);
                EditorUtility.DisplayProgressBar("Delete Old File", string.Format("{0}/{1}  {2}", index, files.Count, item.Value.Name), index / (float)files.Count);
            }
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Update Resources Files", "Update Success!", "OK");
        }

        [MenuItem("AssetBundles/AssetBundle Folder/Open Local AssetBundle Folder")]
        static void OpenLocalAssetBundleFolder()
        {
            string path = Path.GetFullPath(AssetBundleUtility.LocalAssetBundlePath);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/root," + path);
            }
            else
            {
                EditorUtility.DisplayDialog("Open Local AssetBundle Folder", "Path don't exist: " + path, "OK");
            }
            
        }

        [MenuItem("AssetBundles/AssetBundle Folder/Open Output AssetBundle Folder")]
        static void OpenOutputAssetBundleFolder()
        {
            string path = Path.GetFullPath(AssetBundlesOutputPath);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/root," + path);
            }
            else
            {
                EditorUtility.DisplayDialog("Open Output AssetBundle Folder", "Path don't exist: " + path, "OK");
            }
            
        }

        [MenuItem("AssetBundles/AssetBundle Folder/Clear Local AssetBundle Folder")]
        static void ClearLocalAssetBundleFolder()
        {
            if (Directory.Exists(AssetBundleUtility.LocalAssetBundlePath))
            {
                Directory.Delete(AssetBundleUtility.LocalAssetBundlePath, true);
            }
            EditorUtility.DisplayDialog("Clear Local AssetBundle Folder", "Clear Success", "OK");
        }

        [MenuItem("AssetBundles/AssetBundle Folder/Clear Output AssetBundle Folder")]
        static void ClearOutputAssetBundleFolder()
        {
            if (Directory.Exists(AssetBundlesOutputPath))
            {
                Directory.Delete(AssetBundlesOutputPath, true);
            }
            EditorUtility.DisplayDialog("Clear Output AssetBundle Folder", "Clear Success", "OK");
        }
    }
}
