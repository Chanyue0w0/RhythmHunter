using UnityEngine;

namespace RhythmHunter.FightDemo.Art2
{
    // A camera child follows even render-time camera offsets; LateUpdate alone cannot do that.
    [DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class Art2CameraPinnedForeground : MonoBehaviour
    {
        public Camera targetCamera;
        private Transform originalParent;
        private Vector3 originalPosition, originalScale, cameraPosition, cameraScale;
        private Quaternion originalRotation, cameraRotation;
        private float referenceHeight;
        private bool attached;
        private GameObject environment;
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (attached && (!environment || !environment.activeInHierarchy)) { Restore(); return; }
            if (!targetCamera) targetCamera = Camera.main;
            if (!targetCamera || !targetCamera.isActiveAndEnabled) return;
            if (!attached)
            {
                originalParent = transform.parent; environment = originalParent ? originalParent.gameObject : null;
                originalPosition = transform.localPosition; originalRotation = transform.localRotation; originalScale = transform.localScale;
                transform.SetParent(targetCamera.transform, true);
                cameraPosition = transform.localPosition; cameraRotation = transform.localRotation; cameraScale = transform.localScale;
                referenceHeight = ViewHeight(); attached = true;
            }
            float scale = ViewHeight() / Mathf.Max(.001f, referenceHeight);
            transform.localPosition = new Vector3(cameraPosition.x * scale, cameraPosition.y * scale, cameraPosition.z);
            transform.localRotation = cameraRotation;
            transform.localScale = new Vector3(cameraScale.x * scale, cameraScale.y * scale, cameraScale.z);
        }
        private float ViewHeight() => targetCamera.orthographic ? targetCamera.orthographicSize :
            Mathf.Abs(cameraPosition.z) * Mathf.Tan(targetCamera.fieldOfView * Mathf.Deg2Rad * .5f);
        private void Restore()
        {
            if (!attached) return;
            attached = false;
            transform.SetParent(originalParent, false);
            transform.localPosition = originalPosition; transform.localRotation = originalRotation; transform.localScale = originalScale;
        }
        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();
    }
}
