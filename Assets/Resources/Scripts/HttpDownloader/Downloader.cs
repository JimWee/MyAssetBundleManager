using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using UnityEngine.Experimental.Networking;
using AssetBundles;
using System.Net;
using System;

namespace HttpDownloader
{
    public class Downloader : MonoBehaviour
    {

        public string Url = "http://192.168.1.109:7788/";
        public string FileName = "1.zip";
        public GameObject UIBottomMsg;
        public GameObject UIProgress;
        Text mUIText;
        Slider mUISlider;
        Text mUIText2;
        //byte[] mBuff = new byte[1024 * 256];
        UnityWebRequest mUwr;
        bool mIsDownloading = false;
        long mFileSize = 0;
        const int MBSpeed = 1024 * 1024;
        const int KBSpeed = 1024;
        DateTime mServerFileDateTime = DateTime.Now;

        // Use this for initialization
        void Start()
        {
            mUIText = UIProgress.transform.Find("Text").GetComponent<Text>();
            mUISlider = UIProgress.GetComponent<Slider>();
            mUIText2 = UIBottomMsg.GetComponent<Text>();
            //StartCoroutine(GetTextWWW());
            //StartCoroutine(GetText());
            //GetTextHttp();
            if (!Directory.Exists(AssetBundleUtility.LocalAssetBundlePath))
            {
                Directory.CreateDirectory(AssetBundleUtility.LocalAssetBundlePath);
            }
            StartCoroutine(Head());
            //StartCoroutine(Download());
            
        }

        // Update is called once per frame
        void Update()
        {
            if (mIsDownloading && mUwr != null)
            {
                //Debug.Log(mUwr.downloadProgress);
                float progress = mUwr.downloadProgress;
                mUIText.text = string.Format("{0:F}%", progress * 100);
                mUISlider.value = progress;
                DownloadHandlerFile downloadHandler = (DownloadHandlerFile)mUwr.downloadHandler;
                //mUIText2.text = GetDownloadSpeedStr(downloadHandler.GetDownloadSpeed());
                mUIText2.text = mUwr.downloadedBytes.ToString();
            }            
        }

        string GetDownloadSpeedStr(float speed)
        {
            if (speed >= MBSpeed)
            {
                return string.Format("{0:F}MB/s", speed / MBSpeed);
            }
            if (speed >= KBSpeed)
            {
                return string.Format("{0:F}KB/s", speed / KBSpeed);
            }
            return string.Format("{0:F}B/s", speed);
        }


        IEnumerator Head()
        {
            UnityWebRequest uwr = UnityWebRequest.Head(Url + FileName);           
            yield return uwr.Send();
            if (uwr.isError)
            {
                Debug.Log(uwr.error);
            }
            Debug.Log("responseCode:" + uwr.responseCode);
            string strLength = uwr.GetResponseHeader("Content-Length");
            if (!string.IsNullOrEmpty(strLength))
            {
                mFileSize = Convert.ToInt64(strLength);
                Debug.LogFormat("Content - Length: {0}", mFileSize);
            }
            string strDateTime = uwr.GetResponseHeader("Last-Modified");
            if (!string.IsNullOrEmpty(strDateTime))
            {
                mServerFileDateTime = DateTime.Parse(strDateTime);
                Debug.LogFormat("Last-Modified: {0}", mServerFileDateTime.ToString("yyyy.MM.dd HH:mm:ss"));
            }

            StartCoroutine(Download());
        }

        IEnumerator Download()
        {
            string filePath = Path.Combine(AssetBundleUtility.LocalAssetBundlePath, FileName);
            mUwr = new UnityWebRequest(Url + FileName, UnityWebRequest.kHttpVerbGET);
            float startTime = Time.realtimeSinceStartup;
            Debug.LogFormat("startTime: {0}", startTime);
            if (File.Exists(filePath))
            {
                Debug.Log("Continue");
                long fileSize = new FileInfo(filePath).Length;
                if (fileSize == mFileSize)
                {
                    Debug.Log("Has done");
                    yield break;
                }
                //mUwr.downloadHandler = new DownloadHandlerFile(mBuff, filePath, FileMode.Append);
                mUwr.downloadHandler = new DownloadHandlerFile(filePath, FileMode.Append);
                mUwr.SetRequestHeader("Range", string.Format("bytes={0}-", fileSize));
            }
            else
            {
                Debug.Log("New");
                //mUwr.downloadHandler = new DownloadHandlerFile(mBuff, filePath);
                mUwr.downloadHandler = new DownloadHandlerFile(filePath);
            }
            
            mIsDownloading = true;
            yield return mUwr.Send();
            Debug.LogFormat("deltaTime: {0}", Time.realtimeSinceStartup - startTime);
            mIsDownloading = false;
        }

        IEnumerator GetText()
        {
            UnityWebRequest www = UnityWebRequest.Get(Url + FileName);
            //Debug.Log(www.GetRequestHeader("Pragma"));
            //www.SetRequestHeader("Range", "bytes=500-");
            //Debug.Log(www.GetRequestHeader("Range"));
            Debug.Log(Time.realtimeSinceStartup);
            yield return www.Send();

            if (www.isError)
            {
                Debug.Log(www.error);
            }
            else
            {
                Debug.Log("responseCode:" + www.responseCode);
                Debug.Log(Time.realtimeSinceStartup);
                // Or retrieve results as binary data
                byte[] results = www.downloadHandler.data;
                File.WriteAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, FileName), results);
            }
        }

        IEnumerator GetTextWWW()
        {
            WWW www = new WWW(Url + FileName);
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Debug.Log(www.error);
            }
            else if (www.isDone)
            {

                File.WriteAllBytes(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, FileName), www.bytes);
            }
        }

        void GetTextHttp()
        {
            HttpWebRequest www = (HttpWebRequest)HttpWebRequest.Create(Url + FileName);
            www.Method = WebRequestMethods.Http.Post;
            www.AddRange(200, 300);
            using (WebResponse wr = www.GetResponse())
            {
                Stream inStream = wr.GetResponseStream();
                FileStream outStream = new FileStream(Path.Combine(AssetBundleUtility.LocalAssetBundlePath, FileName), FileMode.Create, FileAccess.Write);
                int count = 0;
                int buffSize = 1024 * 1000;
                byte[] buff = new byte[buffSize];
                while ((count = inStream.Read(buff, 0, buffSize)) > 0)
                {
                    outStream.Write(buff, 0, count);
                    outStream.Flush();
                }
                outStream.Close();
            }
        }
    }
}
