using UnityEngine;

namespace Crestforge.Visuals
{
    /// <summary>
    /// Simple floating text animation
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public float floatSpeed = 1f;
        public float lifetime = 1f;

        private float elapsed;
        private Vector3 startPos;
        private TextMesh textMesh;

        private void Start()
        {
            startPos = transform.position;
            textMesh = GetComponent<TextMesh>();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // Float upward
            transform.position = startPos + Vector3.up * elapsed * floatSpeed;

            // Fade out
            if (textMesh != null)
            {
                Color c = textMesh.color;
                c.a = 1f - (elapsed / lifetime);
                textMesh.color = c;
            }

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
