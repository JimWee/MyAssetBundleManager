using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class HUDFPS : MonoBehaviour
{
    public float updateInterval = 0.5f;


    float accum = 0;
    float frames = 0;
    float timeLeft = 0;
    Text mText;

    // Use this for initialization
    void Start()
    {
        mText = GetComponent<Text>();
        timeLeft = updateInterval;
    }

    // Update is called once per frame
    void Update()
    {
        timeLeft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;

        if (timeLeft < 0)
        {
            float fps = accum / frames;
            string text = string.Format("{0:F2} FPS", fps);
            mText.text = text;

            if (fps < 10)
            {
                mText.color = Color.red;
            }
            else if(fps < 30)
            {
                mText.color = Color.yellow;
            }
            else
            {
                mText.color = Color.green;
            }
            timeLeft = updateInterval;
            accum = 0;
            frames = 0;
        }

    }
}
