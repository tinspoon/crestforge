using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Makes a UI element always face the camera
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        private Camera cam;

        private void Start()
        {
            cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
