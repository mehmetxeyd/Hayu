using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float cameraSpeed;


    void Update()
    {
        // Move the camera
        //transform.position += new Vector3(transform.position.x, player.position.y, transform.position.z);
        transform.position += new Vector3(cameraSpeed * Time.deltaTime, 0, 0);

    }

}
