﻿using UnityEngine;
using System;
#if !UNITY_WEBGL && !(UNITY_WSA_8_1 ||  UNITY_WP_8_1 || UNITY_WINRT_8_1) || UNITY_EDITOR
using System.Threading;
using System.IO;
#endif
using System.Collections;
using System.Collections.Generic;


#if (UNITY_WSA_8_1 ||  UNITY_WP_8_1 || UNITY_WINRT_8_1) && !UNITY_EDITOR
 using File = UnityEngine.Windows.File;
 #else
 using File = System.IO.File;
 #endif

#if NETFX_CORE
    #if UNITY_WSA_10_0
    using System.Threading.Tasks;
    using static System.IO.Directory;
    using static System.IO.File;
    using static System.IO.FileStream;
    #endif
#endif

public class testZip : MonoBehaviour
{
#if !UNITY_WEBPLAYER

	//we use some integer to get error codes from the lzma library (look at lzma.cs for the meaning of these error codes)
	private int zres=0;
	
	//for counting the time taken to decompress the 7z file.
	private float t1, t;
	
	private string myFile;
	private WWW www;

    private string log;
	
	private string ppath;
	
	private bool compressionStarted, pass;
	private bool downloadDone;

	//reusable buffers
    private byte[] reusableBuffer, reusableBuffer2, reusableBuffer3;

	//fixed size buffers, that don't get resized, to perform compression/decompression of buffers in them and avoid memory allocations.
	private byte[] fixedInBuffer = new byte[1024*256];
	private byte[] fixedOutBuffer = new byte[1024*768];


    //A single item integer array that changes to the current number of file that get uncompressed of a zip archive.
    //When running the decompress_File function, compare this int to the total number of files returned by the getTotalFiles function
    //to get the progress of the extraction if the zip contains multiple files.
    //If you use multiple threads, remember to use other progress integers for the other threads, otherwise there will be a sharing violation.
    //
    private int[] progress = new int[1];

    //log for output of results
    void plog(string t)
    {
        log += t + "\n"; ;
    }

	void Start(){

		#if (UNITY_WSA_8_1 ||  UNITY_WP_8_1 || UNITY_WINRT_8_1) && !UNITY_EDITOR
			ppath = UnityEngine.Windows.Directory.localFolder;
		#else
			ppath = Application.persistentDataPath;
		#endif

        #if UNITY_STANDALONE_OSX && !UNITY_EDITOR
		     ppath=".";
        #endif

        Debug.Log(ppath);

        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        //various byte buffers for testing
        reusableBuffer = new byte[4096];
        reusableBuffer2 = new byte[0];
        reusableBuffer3 = new byte[0];
		
		Screen.sleepTimeout = SleepTimeout.NeverSleep;

        //call the download coroutine to download a test file
        StartCoroutine(DownloadZipFile());

    }
	

	void Update(){
		if (Input.GetKeyDown(KeyCode.Escape)) { Application.Quit(); }
	}
	
	
	void OnGUI(){
		
		
		if (downloadDone == true){
			GUI.Label(new Rect(10, 0, 250, 30), "package downloaded, ready to extract");
			GUI.Label(new Rect(10, 50, 650, 100), ppath);
		}
		
		
		if (compressionStarted){
            GUI.TextArea(new Rect(10, 160, Screen.width - 20, Screen.height - 170), log);
            GUI.Label(new Rect(Screen.width - 90, 0, 80,40), progress[0].ToString());
		}

        if (downloadDone) {
		    if (GUI.Button(new Rect(10, 90, 230, 50), "start zip test")){
                log = "";
			    compressionStarted = true;
			    DoDecompression();
		    }
			#if (UNITY_IPHONE || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_ANDROID || UNITY_STANDALONE_LINUX || UNITY_EDITOR) && !UNITY_EDITOR_WIN
				if (GUI.Button(new Rect(260, 90, 230, 50), "start File Buffer test")){
					log = "";
					compressionStarted = true;
					DoDecompression_FileBuffer();
				}
			#endif
        }
		
	}



    //Test all the plugin functions.
    //
    void DoDecompression(){

        //zip FILE COMPRESSION/DECOMPRESSION
        //decompress the downloaded file
        t = Time.realtimeSinceStartup;
        zres = lzip.decompress_File(ppath + "/testZip.zip", ppath+"/", progress);
        plog("decompress: " + zres.ToString());

        //extract an entry
        zres = lzip.extract_entry(ppath + "/testZip.zip", "dir1/dir2/test2.bmp", ppath + "/test22.bmp");
        plog("extract entry: " + zres.ToString());
        t1 = Time.realtimeSinceStartup - t;
        plog("time taken: " + t1.ToString());
        plog("");


        //recompress it to test compression
        t = Time.realtimeSinceStartup;
        //compress a file and add it to a new zip
        zres = lzip.compress_File(9, ppath + "/test2Zip.zip", ppath + "/dir1/dir2/test2.bmp",false, "dir1/dir2/test2.bmp");
        plog("compress: " + zres.ToString());

        //append a file to it
        zres = lzip.compress_File(9, ppath + "/test2Zip.zip", ppath + "/dir1/dir2/dir3/Unity_1.jpg",true, "dir1/dir2/dir3/Unity_1.jpg");
        plog("append: " + zres.ToString());
        t1 = Time.realtimeSinceStartup - t;
        plog("time taken: " + t1.ToString());
        plog("");


        //compress a buffer to a file and name it.
        plog( "Buffer2File: "+ lzip.buffer2File(9, ppath + "/test3Zip.zip", "buffer.bin", reusableBuffer).ToString());

        //compress a buffer, name it and append it to an existing zip archive
        plog("Buffer2File append: " + lzip.buffer2File(9, ppath + "/test3Zip.zip", "dir4/buffer.bin", reusableBuffer, true).ToString());
       // Debug.Log(reusableBuffer.Length);
        plog("");

        //get the uncompressed size of a specific file in the zip archive
        plog("get entry size: " + lzip.getEntrySize(ppath + "/testZip.zip", "dir1/dir2/test2.bmp").ToString());
        plog("");

        //extract a file in a zip archive to a byte buffer (referenced buffer method)
        plog("entry2Buffer1: "+ lzip.entry2Buffer(ppath + "/testZip.zip","dir1/dir2/test2.bmp",ref reusableBuffer2).ToString() );
       // File.WriteAllBytes(ppath + "/out.bmp", reusableBuffer2);


        //extract a file in a zip archive to a byte buffer (new buffer method)
        var newBuffer = lzip.entry2Buffer(ppath + "/testZip.zip", "dir1/dir2/test2.bmp");
        zres = 0;
        if (newBuffer != null) zres = 1;
        plog("entry2Buffer2: "+ zres.ToString());
        // write a file out to confirm all was ok
        // File.WriteAllBytes(ppath + "/out.bmp", reusableBuffer2);
        plog("");



		//FIXED BUFFER FUNCTIONS:
		int compressedSize = lzip.compressBufferFixed(newBuffer, ref fixedInBuffer, 10);
		plog(" # Compress Fixed size Buffer: " + compressedSize.ToString());

		if(compressedSize>0){
			int decommpressedSize = lzip.decompressBufferFixed(fixedInBuffer, ref fixedOutBuffer);
			if(decommpressedSize > 0) plog(" # Decompress Fixed size Buffer: " + decommpressedSize.ToString());
		}
		plog("");

        //compress a buffer into a referenced buffer
        pass = lzip.compressBuffer(reusableBuffer2, ref reusableBuffer3, 9);
        plog("compressBuffer1: " + pass.ToString());
        // write a file out to confirm all was ok
        //File.WriteAllBytes(ppath + "/out.bin", reusableBuffer3);

        //compress a buffer and return a new buffer with the compresed data.
        newBuffer = lzip.compressBuffer(reusableBuffer2,9);
        zres = 0;
        if (newBuffer != null) zres = 1;
        plog("compressBuffer2: " + zres.ToString());
        plog("");


        //decompress a previously compressed buffer into a referenced buffer
        pass = lzip.decompressBuffer(reusableBuffer3, ref reusableBuffer2);
        plog("decompressBuffer1: " + pass.ToString());
        //Debug.Log(reusableBuffer2.Length);
        // write a file out to confirm all was ok
        //File.WriteAllBytes(ppath + "/out.bmp", reusableBuffer2);
        zres = 0;
        if (newBuffer != null) zres = 1;

        //decompress a previously compressed buffer into a new returned buffer
        newBuffer = lzip.decompressBuffer(reusableBuffer3);
        plog("decompressBuffer2: " + zres.ToString());
        plog("");

        //get file info of the zip file (names, uncompressed and compressed sizes)
		//on WSA call the getFileInfo should use as the log path always the Application.persistentDataPath path!
		#if UNITY_WSA && !UNITY_EDITOR
			plog( "total bytes: " + lzip.getFileInfo(@ppath + "/testZip.zip", @UnityEngine.Windows.Directory.localFolder+"/").ToString());
		#else
			plog( "total bytes: " + lzip.getFileInfo(ppath + "/testZip.zip", ppath).ToString());
		#endif
        

        //Look through the ninfo, uinfo and cinfo Lists where the file names and sizes are stored.
        if (lzip.ninfo != null) {
            for (int i = 0; i < lzip.ninfo.Count; i++) {
                log += lzip.ninfo[i] + " - " + lzip.uinfo[i] + " / " + lzip.cinfo[i] + "\n";
            }
        }
        plog("");

        //Recursively compress a directory
        t = Time.realtimeSinceStartup;
        lzip.compressDir(ppath + "/dir1", 10, ppath + "/recursive.zip");
        t1 = Time.realtimeSinceStartup - t;
        plog("recursive compress time: "+t1.ToString()+" - no. of files: "+lzip.cProgress.ToString());

        //decompress the above compressed zip to make sure all was ok.
        t = Time.realtimeSinceStartup;
        lzip.decompress_File(ppath + "/recursive.zip", ppath+"/recursive/", progress);
        t1 = Time.realtimeSinceStartup - t;
        plog("decompress recursive time: "+t1.ToString());

        //multithreading example to show progress of extraction, using the ref progress int
        //in this example it happens to fast, because I didn't want the user to download a big file with many entrie.
		#if !UNITY_WEBGL || UNITY_EDITOR
			#if !NETFX_CORE
				Thread th = new Thread(decompressFunc); th.Start();
			#endif
			#if NETFX_CORE && UNITY_WSA_10_0
				Task task = new Task(new Action(decompressFunc)); task.Start();
			#endif
		#endif

    }

    void decompressFunc()
    {
        int res = lzip.decompress_File(ppath + "/recursive.zip", ppath + "/recursive/", progress);
        if (res == 1) plog("multithreaded ok"); else plog("multithreaded error: "+res.ToString());
    }


	//these functions demonstrate how to extract data from a zip file in a byte buffer.
	//
	void DoDecompression_FileBuffer() {
		#if (UNITY_IPHONE || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_ANDROID || UNITY_STANDALONE_LINUX || UNITY_EDITOR) && !UNITY_EDITOR_WIN
			//we read a downloaded zip from the Persistent data path. It could be also a file in a www.bytes buffer.
			var fileBuffer = File.ReadAllBytes(ppath + "/testZip.zip");

			//zip FILE COMPRESSION/DECOMPRESSION
			//decompress the downloaded file
			t = Time.realtimeSinceStartup;
			zres = lzip.decompress_File(null, ppath+"/", progress, fileBuffer);
			plog("decompress: " + zres.ToString());

			//extract an entry
			zres = lzip.extract_entry(null, "dir1/dir2/test2.bmp", ppath + "/test22.bmp", fileBuffer);
			plog("extract entry: " + zres.ToString());
			t1 = Time.realtimeSinceStartup - t;
			plog("time taken: " + t1.ToString());
			plog("");

			//get the uncompressed size of a specific file in the zip archive
			plog("get entry size: " + lzip.getEntrySize(null, "dir1/dir2/test2.bmp", fileBuffer).ToString());
			plog("");

			//extract a file in a zip archive to a byte buffer (referenced buffer method)
			plog("entry2Buffer1: "+ lzip.entry2Buffer(null,"dir1/dir2/test2.bmp",ref reusableBuffer2, fileBuffer).ToString() );
		   // File.WriteAllBytes(ppath + "/out.bmp", reusableBuffer2);


			//extract a file in a zip archive to a byte buffer (new buffer method)
			var newBuffer = lzip.entry2Buffer(null, "dir1/dir2/test2.bmp", fileBuffer);
			zres = 0;
			if (newBuffer != null) zres = 1;
			plog("entry2Buffer2: "+ zres.ToString());
			// write a file out to confirm all was ok
			// File.WriteAllBytes(ppath + "/out.bmp", reusableBuffer2);
			plog("");


			//get file info of the zip file (names, uncompressed and compressed sizes)
			plog( "total bytes: " + lzip.getFileInfo(null, ppath, fileBuffer).ToString());

			//Look through the ninfo, uinfo and cinfo Lists where the file names and sizes are stored.
			if (lzip.ninfo != null) {
				for (int i = 0; i < lzip.ninfo.Count; i++) {
					log += lzip.ninfo[i] + " - " + lzip.uinfo[i] + " / " + lzip.cinfo[i] + "\n";
				}
			}
			plog("");

		#endif
	}

    IEnumerator DownloadZipFile()
    {

        Debug.Log("starting download");

		myFile = "testZip.zip";

        //make sure a previous 7z file having the same name with the one we want to download does not exist in the ppath folder
        if (File.Exists(ppath + "/" + myFile)) File.Delete(ppath + "/" + myFile);

        //replace the link to the 7z file with your own (although this will work also)
        www = new WWW("https://dl.dropboxusercontent.com/u/13373268/tests/" + myFile);

        yield return www;

        if (www.error != null) Debug.Log(www.error);

        downloadDone = true;

        //write the downloaded 7z file to the ppath directory so we can have access to it
        //depending on the Install Location you have set for your app, set the Write Access accordingly!
		File.WriteAllBytes(ppath + "/" + myFile, www.bytes);

        www.Dispose();
        www = null;


    }

#else
    void Start(){
        Debug.Log("Does not work on WebPlayer!");
    }
#endif

}

