using UnityEngine;
using UnityEngine.Experimental.Networking;
using System.Collections;
using System.IO;


namespace HttpDownloader
{
    class DownloadHandlerFile : DownloadHandlerScript
    {
        FileStream mFileStream;
        long mLocalFileSize = 0;
        long mTotalFileSize = 0;
        long mCurrentSize = 0;

        public DownloadHandlerFile(string filePath, FileMode fileMode = FileMode.Create) 
            : base()
        {
            Init(filePath, fileMode);
        }

        public DownloadHandlerFile(byte[] buff, string filePath, FileMode fileMode = FileMode.Create) 
            : base(buff)
        {
            Init(filePath, fileMode);
        }

        private void Init(string filePath, FileMode fileMode)
        {
            mFileStream = new FileStream(filePath, fileMode);
            if (fileMode == FileMode.Append)
            {
                mLocalFileSize = (File.Exists(filePath)) ? (new FileInfo(filePath)).Length : 0;
            }
        }

        protected override byte[] GetData()
        {
            return null;    
        }

        protected override string GetText()
        {
            return null;
        }

        protected override float GetProgress()
        {
            if (mTotalFileSize != 0)
            {
                return (mCurrentSize + mLocalFileSize) / (float)(mTotalFileSize + mLocalFileSize);
            }
            else
            {
                return 0;
            }
            
        }

        protected override void ReceiveContentLength(int contentLength)
        {
            mTotalFileSize = contentLength;
            Debug.Log(string.Format("DownloadHandlerFiles :: ReceiveContentLength - length {0}", contentLength));
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || data.Length < 1)
            {
                Debug.Log("DownloadHandlerFile :: ReceiveData - received a null/empty buffer");
                return false;
            }

            //Debug.Log(string.Format("DownloadHandlerFile :: ReceiveData - received {0} bytes", dataLength));
            mFileStream.Write(data, 0, dataLength);
            mFileStream.Flush();

            mCurrentSize += dataLength;

            return true;
        }

        protected override void CompleteContent()
        {
            Debug.Log("DownloadHandlerFile :: CompleteContent - DOWNLOAD COMPLETE!");
            mFileStream.Close();
        }
    }
}
