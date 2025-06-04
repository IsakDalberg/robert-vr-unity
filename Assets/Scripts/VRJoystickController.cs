using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRJoystickController : MonoBehaviour
{
    public Transform joystick;  // Joystick cylinder
    public string serverUrl = "http://192.168.144.101/control_motor";

    public float maxTilt = 30f; // Maximum tilt angle
    public float returnSpeed = 5f; // Speed of return to center

    private bool isGrabbed = false;
    private float sendInterval = 0.1f;
    private float timer = 0f;
    private Quaternion defaultRotation;

    private XRGrabInteractable grabInteractable;

    void Start()
    {
        defaultRotation = joystick.localRotation;
        grabInteractable = joystick.GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    void Update()
    {
        // Limit joystick's rotation when grabbed
        if (isGrabbed)
        {
            Vector3 euler = joystick.localEulerAngles;
            euler.x = ClampAngle(euler.x, -maxTilt, maxTilt);
            euler.z = ClampAngle(euler.z, -maxTilt, maxTilt);
            euler.y = 0f; // Lock Y axis
            joystick.localEulerAngles = euler;
        }
        else
        {
            // Smooth return to default rotation
            joystick.localRotation = Quaternion.Slerp(joystick.localRotation, defaultRotation, Time.deltaTime * returnSpeed);
        }

        // Convert joystick tilt to motor speeds
        float leftMotor = Mathf.Clamp(joystick.localEulerAngles.z > 180 ? joystick.localEulerAngles.z - 360 : joystick.localEulerAngles.z, -maxTilt, maxTilt) / maxTilt;
        float rightMotor = Mathf.Clamp(joystick.localEulerAngles.x > 180 ? joystick.localEulerAngles.x - 360 : joystick.localEulerAngles.x, -maxTilt, maxTilt) / maxTilt;

        // Send motor data at interval
        timer += Time.deltaTime;
        if (timer >= sendInterval)
        {
            if (isGrabbed == false) 
            {
                rightMotor = 0;
                leftMotor = 0;
            }
            StartCoroutine(SendMotorData(rightMotor, leftMotor));
            timer = 0f;
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
    }

    float ClampAngle(float angle, float min, float max)
    {
        angle = angle > 180 ? angle - 360 : angle;
        return Mathf.Clamp(angle, min, max);
    }

    IEnumerator SendMotorData(float left, float right)
    {
        string json = JsonUtility.ToJson(new MotorData(left, right));
        UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
        byte[] bodyRaw = new System.Text.UTF8Encoding().GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Motor control sent: {request.downloadHandler.text}");
        }
        else
        {
            Debug.LogError($"Error sending motor control: {request.error}");
        }
    }

    [System.Serializable]
    public class MotorData
    {
        public float left;
        public float right;

        public MotorData(float left, float right)
        {
            this.left = left;
            this.right = right;
        }
    }
}
