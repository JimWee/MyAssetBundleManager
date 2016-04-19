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
        public static string LocalAssetBundlePath = Application.persistentDataPath + "/Patches";
        public static string VersionFileName = "version";
        public static bool ForceRedowload = false;
        public static string SecretKey = "12345678";

        public static bool ResolveEncryptedVersionData(byte[] bytes, ref Dictionary<string, AssetBundleInfo> assetBundleInfos, out string error)
        {
            return ResolveDecryptedVersionData(Decrypt(bytes, SecretKey), ref assetBundleInfos, out error);
        }

        public static bool ResolveDecryptedVersionData(byte[] bytes, ref Dictionary<string, AssetBundleInfo> assetBundleInfos, out string error)
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
                return false;
            }
        }

        public static bool CheckLocalAssetBundles(out Dictionary<string, AssetBundleInfo> errorAssetBundleInfos)
        {
            errorAssetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            string versionFilePath = Path.Combine(LocalAssetBundlePath, VersionFileName);

            if (!File.Exists(versionFilePath))
            {
                Debug.Log(string.Format("File dosen't exist: {0}", VersionFileName));
                return false;
            }

            string error;
            Dictionary<string, AssetBundleInfo> assetBundleInfos = new Dictionary<string, AssetBundleInfo>();
            if (!ResolveEncryptedVersionData(File.ReadAllBytes(versionFilePath), ref assetBundleInfos, out error))
            {
                Debug.Log("resolve local version file failed!");
                return false;
            }
            foreach (var item in assetBundleInfos)
            {
                string assetBundleFilePath = Path.Combine(LocalAssetBundlePath, item.Value.AssetBundleName);
                if (!File.Exists(assetBundleFilePath))
                {
                    errorAssetBundleInfos.Add(item.Key, item.Value);
                }
                else
                {
                    string MD5 = GetMD5HashFromFile(assetBundleFilePath);
                    if (!MD5.Equals(item.Value.MD5))
                    {
                        errorAssetBundleInfos.Add(item.Key, item.Value);
                    }
                }
            }
            return assetBundleInfos.Count == 0;
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
            cs.Flush();
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
            cs.Flush();
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