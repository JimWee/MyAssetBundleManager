using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AssetBundles
{
    public class BuildAssetBundles
    {        
        public const string AssetBundlesOutputPath = "AssetBundles";
        public const string PatchesOutputPath = "Patches";
        public const string ChangeLogFileName = "ChangeLog.txt";
        public static string ZipFileOutputPath = Application.streamingAssetsPath;
        public const int LevelOfCompression = 9; //(0-10)

        const string kSimulationMode = "AssetBundles/Simulation Mode";
        [MenuItem(kSimulationMode)]
        public static void ToggleSimulationMode()
        {
            AssetBundleUtility.SimulateAssetBundleInEditor = !AssetBundleUtility.SimulateAssetBundleInEditor;
        }

        [MenuItem(kSimulationMode, true)]
        public static bool ToggleSimulationModeValidate()
        {
            Menu.SetChecked(kSimulationMode, AssetBundleUtility.SimulateAssetBundleInEditor);
            return true;
        }

        [MenuItem("AssetBundles/Build AssetBundles")]
        static public void Build()
        {
            string outputPath = Path.Combine(AssetBundlesOutputPath, AssetBundleUtility.GetPlatformName());
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
                
            List<string> paths = new List<string>();
            paths.Add(AssetBundleUtility.GetPlatformName());

            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();

            string[] lookFor = new string[] { AssetBundleUtility.AssetBundleResourcesPath };           
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
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                FileStream fs = new FileStream(Path.Combine(outputPath, path), FileMode.Open);
                sb.AppendFormat("{0}\t{1}\t{2}\n", path, AssetBundleUtility.GetMD5HashFromFileStream(fs), fs.Length);
                fs.Close();
                EditorUtility.DisplayProgressBar("Compute MD5", string.Format("{0}/{1}  {2}", i + 1, paths.Count, path), (i + 1) / (float)paths.Count);
            }            
            File.WriteAllBytes(Path.Combine(outputPath, AssetBundleUtility.VersionFileName), Encoding.UTF8.GetBytes(sb.ToString()));
            EditorUtility.ClearProgressBar();

            if(!EditorUtility.DisplayDialog("Build AssetBundles", "Build Success!", "OK", "Open Contain Folder"))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + Path.GetFullPath(outputPath));
            }
        }

        static string AssetPathToAssetBundleName(string assetPath)
        {     
            string assetbundleName = assetPath.Replace(AssetBundleUtility.AssetBundleResourcesPath + "/", "");
            return assetbundleName.Substring(0, assetbundleName.LastIndexOf('.'));            
        }

        [MenuItem("AssetBundles/Update Resources Files")]
        static public void UpdateResourcesFiles()
        {
            string patchesOutputPath = Path.Combine(PatchesOutputPath, AssetBundleUtility.GetPlatformName());
            string assetBundlesOutputPath = Path.Combine(AssetBundlesOutputPath, AssetBundleUtility.GetPlatformName());

            if (!Directory.Exists(patchesOutputPath))
            {
                Directory.CreateDirectory(patchesOutputPath);
            }

            //获取资源目录下现有文件列表
            DirectoryInfo dirInfo = new DirectoryInfo(patchesOutputPath);
            FileInfo[] fileInfos = dirInfo.GetFiles();
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
            foreach (var item in fileInfos)
            {
                files.Add(item.Name, item);
            }

            //获取最新AssetBundle文件信息
            string error = string.Empty;
            Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            byte[] bytes = File.ReadAllBytes(Path.Combine(assetBundlesOutputPath, AssetBundleUtility.VersionFileName));            
            if (!AssetBundleUtility.ResolveDecryptedVersionData(bytes, ref assetBundleInfos, out error))
            {
                Debug.LogError("resolve version file failed: " + error);
                return;
            }

            StringBuilder keepFilesSB = new StringBuilder("Keep Files:\n");
            StringBuilder addFilesSB = new StringBuilder("Add Files:\n");
            StringBuilder deleteFilesSB = new StringBuilder("Delet Files:\n");

            int index = 0;
            foreach (var item in assetBundleInfos)
            {
                index++;
                if (files.ContainsKey(item.Value.MD5))//已有文件
                {
                    files.Remove(item.Value.MD5);
                    keepFilesSB.AppendFormat("{0}\t{1}\n", item.Key, item.Value.MD5);
                }
                else//新文件
                {
                    File.Copy(Path.Combine(assetBundlesOutputPath, item.Key), Path.Combine(patchesOutputPath, item.Value.MD5), true);
                    addFilesSB.AppendFormat("{0}\t{1}\n", item.Key, item.Value.MD5);
                }
                EditorUtility.DisplayProgressBar("Copy New File", string.Format("{0}/{1}  {2}", index, assetBundleInfos.Count, item.Value.MD5), index / (float)assetBundleInfos.Count);
            }
            //删除旧文件
            index = 0;
            foreach (var item in files)
            {
                index++;
                File.Delete(item.Value.FullName);
                deleteFilesSB.AppendLine(item.Key);
                EditorUtility.DisplayProgressBar("Delete Old File", string.Format("{0}/{1}  {2}", index, files.Count, item.Value.Name), index / (float)files.Count);
            }

            //写入version文件
            File.WriteAllBytes(Path.Combine(patchesOutputPath, AssetBundleUtility.VersionFileName), AssetBundleUtility.Encrypt(bytes, AssetBundleUtility.SecretKey));

            //changelog
            File.WriteAllBytes(Path.Combine(PatchesOutputPath, AssetBundleUtility.GetPlatformName() + ChangeLogFileName), Encoding.UTF8.GetBytes(keepFilesSB.ToString() + addFilesSB.ToString() + deleteFilesSB.ToString()));

            EditorUtility.ClearProgressBar();
            if(!EditorUtility.DisplayDialog("Update Resources Files", "Update Success!", "OK", "Open Contain Folder"))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + Path.GetFullPath(patchesOutputPath));
            }
        }

        [MenuItem("AssetBundles/Build Resources Zip")]
        static void BuildResourcesZip()
        {
            string inputPath = Path.Combine(PatchesOutputPath, AssetBundleUtility.GetPlatformName());
            string outputFileName = Path.Combine(ZipFileOutputPath, AssetBundleUtility.ZipFileName);

            if (!Directory.Exists(ZipFileOutputPath))
            {
                Directory.CreateDirectory(ZipFileOutputPath);
            }
            if (File.Exists(outputFileName))
            {
                File.Delete(outputFileName);
            }

            int progress = 0;
            DirectoryInfo dirInfo = new DirectoryInfo(inputPath);
            FileInfo[] fileInfos = dirInfo.GetFiles();
            foreach (var item in fileInfos)
            {
                progress++;
                EditorUtility.DisplayProgressBar("Build Resources Zip", string.Format("{0}/{1}  {2}", progress, fileInfos.Length, item.Name), progress / (float)fileInfos.Length);
                lzip.compress_File(LevelOfCompression, outputFileName, item.FullName, true);
            }
            EditorUtility.ClearProgressBar();
            if(!EditorUtility.DisplayDialog("Build Resources Zip ", "Build Success!", "OK", "Open Contain Folder"))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + Path.GetFullPath(outputFileName));
            }
        }


        [MenuItem("AssetBundles/AssetBundle Folder/Open Local Patches Folder")]
        static void OpenLocalPatchesFolder()
        {
            string path = Path.GetFullPath(AssetBundleUtility.LocalAssetBundlePath);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
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
                System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
            }
            else
            {
                EditorUtility.DisplayDialog("Open Output AssetBundle Folder", "Path don't exist: " + path, "OK");
            }
            
        }

        [MenuItem("AssetBundles/AssetBundle Folder/Open Output Patches Folder")]
        static void OpenOutputPatchesFolder()
        {
            string path = Path.GetFullPath(PatchesOutputPath);
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
            }
            else
            {
                EditorUtility.DisplayDialog("Open Output Patches Folder", "Path don't exist: " + path, "OK");
            }

        }

        [MenuItem("AssetBundles/AssetBundle Folder/Clear Local Patches Folder")]
        static void ClearLocalPatchesFolder()
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
