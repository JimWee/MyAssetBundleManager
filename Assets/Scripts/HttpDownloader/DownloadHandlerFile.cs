using UnityEngine;
using UnityEngine.Experimental.Networking;
using System.Collections;
using System.IO;


namespace HttpDownloader
{
    class DownloadHandlerFile : DownloadHandlerScript
    {
        FileStream mFileStream;

        public DownloadHandlerFile(string filePath, FileMode fileMode = FileMode.Create) 
            : base()
        {
            mFileStream = new FileStream(filePath, fileMode);
        }

        public DownloadHandlerFile(byte[] buff, string filePath, FileMode fileMode = FileMode.Create) 
            : base(buff)
        {
            mFileStream = new FileStream(filePath, fileMode);
        }

        protected override byte[] GetData()
        {
            return null;    
        }

        protected override string GetText()
        {
            return null;
        }

        protected override void ReceiveContentLength(int contentLength)
        {
            base.ReceiveContentLength(contentLength);
            Debug.Log(string.Format("DownloadHandlerFiles :: ReceiveContentLength - length {0}", contentLength));
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || data.Length < 1)
            {
                Debug.Log("DownloadHandlerFile :: ReceiveData - received a null/empty buffer");
                return false;
            }

            Debug.Log(string.Format("DownloadHandlerFile :: ReceiveData - received {0} bytes", dataLength));
            mFileStream.Write(data, 0, dataLength);
            mFileStream.Flush();
            return true;
        }

        protected override void CompleteContent()
        {
            Debug.Log("DownloadHandlerFile :: CompleteContent - DOWNLOAD COMPLETE!");
            mFileStream.Close();
        }
    }
}
