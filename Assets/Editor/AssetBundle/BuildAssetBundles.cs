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

            StringBuilder sb = new StringBuilder(DateTime.Now.ToString("yyyyMMddHHmmss"));
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                FileStream fs = new FileStream(Path.Combine(outputPath, path), FileMode.Open);
                sb.AppendFormat("\n{0}\t{1}\t{2}", path, AssetBundleUtility.GetMD5HashFromFileStream(fs), fs.Length);
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
            string patchesOutputPathPlatform = Path.Combine(PatchesOutputPath, AssetBundleUtility.GetPlatformName());
            string resourcesOutputPath = Path.Combine(patchesOutputPathPlatform, AssetBundleUtility.ResourcesFolderName);
            string assetBundlesOutputPath = Path.Combine(AssetBundlesOutputPath, AssetBundleUtility.GetPlatformName());

            if (!Directory.Exists(resourcesOutputPath))
            {
                Directory.CreateDirectory(resourcesOutputPath);
            }

            //获取资源目录下现有文件列表
            DirectoryInfo dirInfo = new DirectoryInfo(resourcesOutputPath);
            FileInfo[] fileInfos = dirInfo.GetFiles();
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
            foreach (var item in fileInfos)
            {
                files.Add(item.Name, item);
            }
            if (files.ContainsKey(AssetBundleUtility.VersionFileName))
            {
                files.Remove(AssetBundleUtility.VersionFileName);
            }
            
            string error = string.Empty;

            //获取现有文件的资源版本
            Int64 versionIDOld = 0;
            string oldVersionFilePath = Path.Combine(resourcesOutputPath, AssetBundleUtility.VersionFileName);
            if (File.Exists(oldVersionFilePath))
            {
                Dictionary<string, AssetBundleInfo> assetBundleInfosOld = new Dictionary<string, AssetBundleInfo>();
                byte[] bytesOld = File.ReadAllBytes(oldVersionFilePath);
                if (!AssetBundleUtility.ResolveEncryptedVersionData(bytesOld, ref assetBundleInfosOld, out versionIDOld, out error))
                {
                    Debug.LogError("resolve old version file failed: " + error);
                    return;
                }
            }

            //获取最新AssetBundle文件信息
            Int64 versionID;
            Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            byte[] bytes = File.ReadAllBytes(Path.Combine(assetBundlesOutputPath, AssetBundleUtility.VersionFileName));            
            if (!AssetBundleUtility.ResolveDecryptedVersionData(bytes, ref assetBundleInfos, out versionID, out error))
            {
                Debug.LogError("resolve version file failed: " + error);
                return;
            }

            //StringBuilder keepFilesSB = new StringBuilder("Keep Files:\n");
            StringBuilder addFilesSB = new StringBuilder("Add Files:\n");
            StringBuilder deleteFilesSB = new StringBuilder("Delet Files:\n");

            List<AssetBundleInfo> addFiles = new List<AssetBundleInfo>();

            int index = 0;
            foreach (var item in assetBundleInfos)
            {
                index++;
                if (files.ContainsKey(item.Value.MD5))//已有文件
                {
                    files.Remove(item.Value.MD5);
                    //keepFilesSB.AppendFormat("\t{0}\t{1}\n", item.Key, item.Value.MD5);
                }
                else//新文件
                {
                    File.Copy(Path.Combine(assetBundlesOutputPath, item.Key), Path.Combine(resourcesOutputPath, item.Value.MD5), true);
                    addFiles.Add(item.Value);
                    addFilesSB.AppendFormat("\t{0}\t{1}\n", item.Key, item.Value.MD5);
                }
                EditorUtility.DisplayProgressBar("Copy New File", string.Format("{0}/{1}  {2}", index, assetBundleInfos.Count, item.Value.MD5), index / (float)assetBundleInfos.Count);
            }
            //删除旧文件
            index = 0;
            foreach (var item in files)
            {
                index++;
                File.Delete(item.Value.FullName);
                deleteFilesSB.AppendFormat("\t{0}\n", item.Key);
                EditorUtility.DisplayProgressBar("Delete Old File", string.Format("{0}/{1}  {2}", index, files.Count, item.Value.Name), index / (float)files.Count);
            }

            string hintText = "Already up-to-date";

            //有增加文件或删除文件，则认为文件有变动
            if (addFiles.Count > 0 || files.Count > 0)
            {
                //写入version文件
                File.WriteAllBytes(Path.Combine(resourcesOutputPath, AssetBundleUtility.VersionFileName), AssetBundleUtility.Encrypt(bytes, AssetBundleUtility.SecretKey));

                //生成补丁包
                string pathFileName = Path.Combine(patchesOutputPathPlatform, string.Format("{0}-{1}.zip", versionIDOld, versionID));
                index = 0;
                foreach (var item in addFiles)
                {
                    index++;
                    EditorUtility.DisplayProgressBar("Build Patch File", string.Format("{0}/{1}  {2}", index, addFiles.Count, item.MD5), index / (float)addFiles.Count);
                    int res = lzip.compress_File(LevelOfCompression, pathFileName, Path.Combine(resourcesOutputPath, item.MD5), true);
                    if (res < 0)
                    {
                        EditorUtility.DisplayDialog("Update Resources Files", string.Format("Compression Failed - fileName: {0}, errorCode: {1}", item.MD5, res), "OK");
                        return;
                    }
                }
                int res1 = lzip.compress_File(LevelOfCompression, pathFileName, Path.Combine(resourcesOutputPath, AssetBundleUtility.VersionFileName), true);
                if (res1 < 0)
                {
                    EditorUtility.DisplayDialog("Update Resources Files", string.Format("Compression Failed - fileName: {0}, errorCode: {1}", AssetBundleUtility.VersionFileName, res1), "OK");
                    return;
                }

                //patchList
                string patchFileMD5 = AssetBundleUtility.GetMD5HashFromFile(pathFileName);
                long patchFileSize = new FileInfo(pathFileName).Length;
                File.AppendAllText(Path.Combine(patchesOutputPathPlatform, AssetBundleUtility.PatchListFileName),
                    string.Format("{0}\t{1}\t{2}\t{3}\n", versionIDOld, versionID, patchFileMD5, patchFileSize));

                //changelog
                string changeLog = string.Format("Version.{0} - Version.{1}\n\n{2}{3}\n\n\n", versionIDOld, versionID, addFilesSB.ToString(), deleteFilesSB.ToString());
                File.AppendAllText(Path.Combine(patchesOutputPathPlatform, ChangeLogFileName), changeLog, Encoding.UTF8);

                hintText = string.Format("Update Success: {0}", pathFileName);
            }

            EditorUtility.ClearProgressBar();
            if(!EditorUtility.DisplayDialog("Update Resources Files", hintText, "OK", "Open Contain Folder"))
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select," + Path.GetFullPath(patchesOutputPathPlatform));
            }
        }

        [MenuItem("AssetBundles/Build Resources Zip")]
        static void BuildResourcesZip()
        {
            string outputPath = Path.Combine(PatchesOutputPath, AssetBundleUtility.GetPlatformName());
            string inputPath = Path.Combine(outputPath, AssetBundleUtility.ResourcesFolderName);            

            //获取资源版本号
            string error;
            Int64 versionID;
            Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            byte[] bytes = File.ReadAllBytes(Path.Combine(inputPath, AssetBundleUtility.VersionFileName));
            if (!AssetBundleUtility.ResolveEncryptedVersionData(bytes, ref assetBundleInfos, out versionID, out error))
            {
                Debug.LogError("resolve version file failed: " + error);
                return;
            }
            string outputFileName = Path.Combine(outputPath, string.Format("{0}_{1}", AssetBundleUtility.ZipFileName, versionID));

            int progress = 0;
            DirectoryInfo dirInfo = new DirectoryInfo(inputPath);
            FileInfo[] fileInfos = dirInfo.GetFiles();
            foreach (var item in fileInfos)
            {
                progress++;
                EditorUtility.DisplayProgressBar("Build Resources Zip", string.Format("{0}/{1}  {2}", progress, fileInfos.Length, item.Name), progress / (float)fileInfos.Length);
                int res = lzip.compress_File(LevelOfCompression, outputFileName, item.FullName, true);
                if (res < 0)
                {
                    EditorUtility.DisplayDialog("Build Resources Zip", string.Format("Build Failed - fileName: {0}, errorCode: {1}", item.FullName, res), "OK");
                    return;
                }
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
