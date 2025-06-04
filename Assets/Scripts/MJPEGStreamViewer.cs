using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

public class MJPEGStreamViewer : MonoBehaviour
{
    public RawImage rawImage;
    public string streamUrl = "http://192.168.144.101:5000/video_feed";

    private Texture2D texture;
    private HttpWebRequest request;
    private HttpWebResponse response;
    private Stream stream;
    private byte[] buffer = new byte[4096];
    private MemoryStream imageStream = new MemoryStream();

    private bool capturingImage = false;

    // Thread-safe queue for frames
    private Queue<byte[]> frameQueue = new Queue<byte[]>();
    private object queueLock = new object();

    void Start()
    {
        texture = new Texture2D(2, 2);
        rawImage.texture = texture;

        // Start background stream reading
        Thread backgroundThread = new Thread(ReadStream);
        backgroundThread.IsBackground = true;
        backgroundThread.Start();

        // Start Unity coroutine to apply frames
        StartCoroutine(UpdateFrame());
    }

    IEnumerator UpdateFrame()
    {
        while (true)
        {
            byte[] jpegData = null;

            lock (queueLock)
            {
                if (frameQueue.Count > 0)
                {
                    jpegData = frameQueue.Dequeue();
                }
            }

            if (jpegData != null)
            {
                texture.LoadImage(jpegData);
                rawImage.texture = texture;
            }

            yield return null; // Continue next frame
        }
    }

    private void ReadStream()
    {
        try
        {
            request = (HttpWebRequest)WebRequest.Create(streamUrl);
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.KeepAlive = true;

            Debug.Log("Connecting to MJPEG...");
            response = (HttpWebResponse)request.GetResponse();
            stream = response.GetResponseStream();
            Debug.Log("✅ Connected!");

            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                    continue;

                bool isFrameStarted = false;
                bool isFrameEnded = false;

                // Process the buffer and ensure valid JPEG frames
                for (int i = 0; i < bytesRead - 1; i++)  // Ensure i + 1 is within bounds
                {
                    // Check for JPEG start (0xFFD8)
                    if (buffer[i] == 0xFF && buffer[i + 1] == 0xD8)
                    {
                        imageStream.SetLength(0); // Clear previous frame data
                        capturingImage = true;
                        isFrameStarted = true; // We've found the start of a frame
                    }

                    if (capturingImage)
                    {
                        imageStream.WriteByte(buffer[i]); // Add data to the current frame
                    }

                    // Check for JPEG end (0xFFD9)
                    if (buffer[i] == 0xFF && buffer[i + 1] == 0xD9)
                    {
                        capturingImage = false;
                        isFrameEnded = true; // We've found the end of a frame
                        byte[] jpegData = imageStream.ToArray();

                        lock (queueLock)
                        {
                            // Clear the queue and enqueue only the latest frame
                            frameQueue.Clear();
                            frameQueue.Enqueue(jpegData);
                        }

                        // Break the loop once we have a complete frame
                        break;
                    }
                }

                // If no complete frame has been detected, continue reading more bytes
                if (!isFrameStarted || !isFrameEnded)
                {
                    // Optionally, you can log this case to debug the issue further.
                    // Debug.Log("Incomplete frame detected. Waiting for more data...");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Stream read error: {e.Message}");
        }
    }



    void OnDestroy()
    {
        stream?.Close();
        response?.Close();
    }
}
