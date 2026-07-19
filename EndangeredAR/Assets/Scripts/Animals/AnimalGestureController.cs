using UnityEngine;

namespace EndangeredAR.Animals
{
    public class AnimalGestureController : MonoBehaviour
    {
        [SerializeField] private float minScale = 0.45f;
        [SerializeField] private float maxScale = 3.2f;
        [SerializeField] private float oneFingerRotationSpeed = 0.25f;
        [SerializeField] private float mouseRotationSpeed = 0.45f;
        [SerializeField] private float mouseWheelScaleSpeed = 0.12f;

        private float baseScaleMagnitude;
        private float pinchStartDistance;
        private float pinchStartScaleMagnitude;
        private float twistStartAngle;
        private float twistStartYRotation;

        private void Awake()
        {
            RefreshBaseScale();
        }

        private void OnEnable()
        {
            RefreshBaseScale();
            pinchStartDistance = 0f;
        }

        public void RefreshBaseScale()
        {
            baseScaleMagnitude = transform.localScale.x <= 0f ? 1f : transform.localScale.x;
        }

        private void Update()
        {
            HandleTouch();
            HandleMouse();
        }

        private void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    transform.Rotate(Vector3.up, -touch.deltaPosition.x * oneFingerRotationSpeed, Space.World);
                }

                return;
            }

            if (Input.touchCount != 2)
            {
                return;
            }

            var first = Input.GetTouch(0);
            var second = Input.GetTouch(1);
            var currentDistance = Vector2.Distance(first.position, second.position);
            var currentAngle = GetTouchAngle(first.position, second.position);

            if (first.phase == TouchPhase.Began || second.phase == TouchPhase.Began || pinchStartDistance <= 0f)
            {
                pinchStartDistance = currentDistance;
                pinchStartScaleMagnitude = transform.localScale.x;
                twistStartAngle = currentAngle;
                twistStartYRotation = transform.eulerAngles.y;
                return;
            }

            if (pinchStartDistance > 1f)
            {
                var scale = pinchStartScaleMagnitude * (currentDistance / pinchStartDistance);
                SetUniformScale(scale);
            }

            var angleDelta = Mathf.DeltaAngle(twistStartAngle, currentAngle);
            var rotation = transform.eulerAngles;
            rotation.y = twistStartYRotation - angleDelta;
            transform.eulerAngles = rotation;
        }

        private void HandleMouse()
        {
            if (Input.GetMouseButton(0))
            {
                transform.Rotate(Vector3.up, -Input.GetAxis("Mouse X") * mouseRotationSpeed * 10f, Space.World);
            }

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                SetUniformScale(transform.localScale.x * (1f + scroll * mouseWheelScaleSpeed));
            }
        }

        private void SetUniformScale(float scale)
        {
            var normalized = Mathf.Clamp(scale / baseScaleMagnitude, minScale, maxScale);
            transform.localScale = Vector3.one * baseScaleMagnitude * normalized;
        }

        private static float GetTouchAngle(Vector2 first, Vector2 second)
        {
            var delta = second - first;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }
    }
}
