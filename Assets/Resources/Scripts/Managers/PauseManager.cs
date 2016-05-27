using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Audio;
using AssetBundles;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PauseManager : MonoBehaviour {
	
	public AudioMixerSnapshot paused;
	public AudioMixerSnapshot unpaused;
	
	Canvas canvas;
	
	void Start()
	{
		canvas = GetComponent<Canvas>();
	}
	
	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
            Pause();
		}
	}
	
	public void Pause()
	{
        canvas.enabled = !canvas.enabled;
        Time.timeScale = Time.timeScale == 0 ? 1 : 0;
		Lowpass ();
		
	}
	
	void Lowpass()
	{
		if (Time.timeScale == 0)
		{
			paused.TransitionTo(.01f);
		}
		
		else
			
		{
			unpaused.TransitionTo(.01f);
		}
	}
	
	public void Quit()
	{
		#if UNITY_EDITOR 
		EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
        AssetBundleLoader.Instance.UnloadScene("Scenes/Level 01");       
	}
}
