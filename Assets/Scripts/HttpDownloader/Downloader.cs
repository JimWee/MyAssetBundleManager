using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Experimental.Networking;
using AssetBundles;
using System.Net;

namespace HttpDownloader
{
    public class Downloader : MonoBehaviour
    {

        public string Url = "http://192.168.1.109:7788/";
        public string FileName = "1.zip";
        byte[] mBuff = new byte[1024 * 1024];
        UnityWebRequest mUwr;
        bool mIsDownloading = false;

        // Use this for initialization
        void Start()
        {
            //StartCoroutine(GetTextWWW());
            //StartCoroutine(GetText());
            //GetTextHttp();
            StartCoroutine(Download());
        }

        // Update is called once per frame
        void Update()
        {
            if (mIsDownloading && mUwr != null)
            {
                Debug.Log(mUwr.downloadProgress);
            }            
        }

        IEnumerator Download()
        {
            mUwr = new UnityWebRequest(Url + FileName, UnityWebRequest.kHttpVerbGET);
            mUwr.downloadHandler = new DownloadHandlerFile(mBuff, Path.Combine(AssetBundleUtility.LocalAssetBundlePath, FileName));
            mIsDownloading = true;
            yield return mUwr.Send();
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
