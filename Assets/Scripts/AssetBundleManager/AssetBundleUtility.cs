using UnityEngine;
#if UNITY_EDITOR	
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System;
using System.Security.Cryptography;

namespace AssetBundles
{
    public class AssetBundleUtility
    {
#if UNITY_EDITOR
        static int m_SimulateAssetBundleInEditor = -1;
        const string kSimulateAssetBundles = "SimulateAssetBundles";
        public static bool SimulateAssetBundleInEditor
        {
            get
            {
                if (m_SimulateAssetBundleInEditor == -1)
                    m_SimulateAssetBundleInEditor = EditorPrefs.GetBool(kSimulateAssetBundles, true) ? 1 : 0;

                return m_SimulateAssetBundleInEditor != 0;
            }
            set
            {
                int newValue = value ? 1 : 0;
                if (newValue != m_SimulateAssetBundleInEditor)
                {
                    m_SimulateAssetBundleInEditor = newValue;
                    EditorPrefs.SetBool(kSimulateAssetBundles, value);
                }
            }
        }
        public const string AssetBundleResourcesPath = "Assets/AssetBundleResources";
        public const string AssetBundleScenesPath = "Assets/AssetBundleScenes";
#endif
        public static string LocalAssetBundlePath = Application.persistentDataPath + "/Patches";
        public static string ResourcesFolderName = "Resources";
        public static string VersionFileName = "version";
        public static string ZipFileName = "Resources.zip";
        public static string PatchListFileName = "PatchesList.txt";
        public static string SecretKey = "12345678";

        /// <summary>
        /// 打印Dictionary<string, AssetBundleInfo>内容
        /// </summary>
        /// <param name="assetBundleInfos"></param>
        public static void PrintAssetBundleInfos(Dictionary<string, AssetBundleInfo> assetBundleInfos)
        {
            if (assetBundleInfos != null)
            {
                StringBuilder sb = new StringBuilder("PrintAssetBundleInfos");
                foreach (var item in assetBundleInfos)
                {
                    sb.AppendFormat("name: {0}, MD5: {1}, size: {2}\n", item.Value.AssetBundleName, item.Value.MD5, item.Value.Size);
                }
                Debug.Log(sb.ToString());
            }
        }

        /// <summary>
        /// 解析加密的version文件
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="assetBundleInfos"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool ResolveEncryptedVersionData(byte[] bytes, ref Dictionary<string, AssetBundleInfo> assetBundleInfos, out Int64 versionID, out string error)
        {
            return ResolveDecryptedVersionData(Decrypt(bytes, SecretKey), ref assetBundleInfos, out versionID, out error);
        }


        /// <summary>
        /// 解析未加密的version文件
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="assetBundleInfos"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool ResolveDecryptedVersionData(byte[] bytes, ref Dictionary<string, AssetBundleInfo> assetBundleInfos, out Int64 versionID, out string error)
        {
            try
            {
                if (assetBundleInfos == null)
                {
                    assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
                }
                else
                {
                    assetBundleInfos.Clear();

                }
                string text = Encoding.UTF8.GetString(bytes);
                string[] items = text.Split('\n');
                versionID = Convert.ToInt64(items[0]);
                foreach (var item in items)
                {
                    string[] infos = item.Split('\t');
                    if (infos != null && infos.Length == 3)
                    {
                        AssetBundleInfo assetBundleInfo;
                        assetBundleInfo.AssetBundleName = infos[0];
                        assetBundleInfo.MD5 = infos[1];
                        assetBundleInfo.Size = Convert.ToInt32(infos[2]);
                        assetBundleInfos.Add(infos[0], assetBundleInfo);
                    }
                }
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = string.Format("Failed resolving version data: {0}", ex.ToString());
                versionID = 0;
                return false;
            }
        }

        /// <summary>
        /// 检查本地资源的完整性, 删除多余文件
        /// </summary>
        /// <param name="checkMD5"></param>
        /// <param name="errorAssetBundleInfos"></param>
        /// <returns></returns>
        public static bool CheckLocalAssetBundles(bool checkMD5, out Dictionary<string, AssetBundleInfo> errorAssetBundleInfos)
        {
            errorAssetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            string versionFilePath = Path.Combine(LocalAssetBundlePath, VersionFileName);

            if (!File.Exists(versionFilePath))
            {
                Debug.LogError(string.Format("File dosen't exist: {0}", VersionFileName));
                return false;
            }

            string error;
            Int64 versionID;
            Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            if (!ResolveEncryptedVersionData(File.ReadAllBytes(versionFilePath), ref assetBundleInfos, out versionID, out error))
            {
                Debug.LogErrorFormat("resolve local version file failed: {0}", error);
                return false;
            }

            //获取资源目录下现有文件列表
            DirectoryInfo dirInfo = new DirectoryInfo(LocalAssetBundlePath);
            FileInfo[] fileInfos = dirInfo.GetFiles();
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();
            foreach (var item in fileInfos)
            {
                files.Add(item.Name, item);
            }
            files.Remove(VersionFileName);

            //检查文件
            foreach (var item in assetBundleInfos)
            {
                FileInfo fileInfo;
                if (files.TryGetValue(item.Value.MD5, out fileInfo))
                {
                    if (checkMD5)
                    {
                        if (GetMD5HashFromFile(fileInfo.FullName) != item.Value.MD5)
                        {
                            errorAssetBundleInfos.Add(item.Key, item.Value);
                        }
                        else
                        {
                            files.Remove(item.Value.MD5);
                        }
                    }
                    else
                    {
                        files.Remove(item.Value.MD5);
                    }
                }
                else
                {
                    errorAssetBundleInfos.Add(item.Key, item.Value);
                }
            }

            //删除多余文件
            foreach (var item in files)
            {
                File.Delete(item.Value.FullName);
            }

            return errorAssetBundleInfos.Count == 0;
        }

        public static string GetMD5HashFromFile(string filePath)
        {
            try
            {
                FileStream file = new FileStream(filePath, FileMode.Open);
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }
        }

        public static string GetMD5HashFromFileStream(FileStream fs)
        {
            try
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(fs);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }
        }

        public static byte[] Encrypt(byte[] bytes, string key)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Key = Encoding.ASCII.GetBytes(key);
            des.IV = Encoding.ASCII.GetBytes(key);
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(bytes, 0, bytes.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }

        public static byte[] Decrypt(byte[] bytes, string key)
        {
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Key = Encoding.ASCII.GetBytes(key);
            des.IV = Encoding.ASCII.GetBytes(key);
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(bytes, 0, bytes.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }

        public static string GetPlatformName()
        {
#if UNITY_EDITOR
            return GetPlatformForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
#else
			return GetPlatformForAssetBundles(Application.platform);
#endif
        }

#if UNITY_EDITOR
        private static string GetPlatformForAssetBundles(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.WebPlayer:
                    return "WebPlayer";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                case BuildTarget.StandaloneOSXUniversal:
                    return "OSX";
                // Add more build targets for your own.
                // If you add more targets, don't forget to add the same platforms to GetPlatformForAssetBundles(RuntimePlatform) function.
                default:
                    return null;
            }
        }
#endif

        private static string GetPlatformForAssetBundles(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.OSXWebPlayer:
                case RuntimePlatform.WindowsWebPlayer:
                    return "WebPlayer";
                case RuntimePlatform.WindowsPlayer:
                    return "Windows";
                case RuntimePlatform.OSXPlayer:
                    return "OSX";
                // Add more build targets for your own.
                // If you add more targets, don't forget to add the same platforms to GetPlatformForAssetBundles(RuntimePlatform) function.
                default:
                    return null;
            }
        }
    }
}