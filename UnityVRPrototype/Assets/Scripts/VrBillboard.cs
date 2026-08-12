using UnityEngine;

public class VrBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }
        transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
    }
}
