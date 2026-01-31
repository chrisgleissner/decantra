using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class BackgroundDrift : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 driftAmplitude = new Vector2(18f, 26f);
        [SerializeField] private Vector2 driftSpeed = new Vector2(0.035f, 0.025f);
        [SerializeField] private float rotationAmplitude = 1.6f;
        [SerializeField] private float rotationSpeed = 0.04f;
        [SerializeField] private Vector2 scaleAmplitude = new Vector2(0.035f, 0.02f);
        [SerializeField] private Vector2 scaleSpeed = new Vector2(0.03f, 0.02f);

        private Vector2 _seed;
        private Vector2 _baseAnchored;
        private Vector3 _baseScale;
        private float _baseRotation;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<RectTransform>();
            }

            _baseAnchored = target != null ? target.anchoredPosition : Vector2.zero;
            _baseScale = target != null ? target.localScale : Vector3.one;
            _baseRotation = target != null ? target.localEulerAngles.z : 0f;
            _seed = new Vector2(Random.value * 10f, Random.value * 10f);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float t = Time.time;
            float offsetX = Mathf.Sin((t + _seed.x) * driftSpeed.x) * driftAmplitude.x;
            float offsetY = Mathf.Cos((t + _seed.y) * driftSpeed.y) * driftAmplitude.y;
            float rot = Mathf.Sin((t + _seed.x) * rotationSpeed) * rotationAmplitude;

            float scaleX = 1f + Mathf.Sin((t + _seed.x) * scaleSpeed.x) * scaleAmplitude.x;
            float scaleY = 1f + Mathf.Cos((t + _seed.y) * scaleSpeed.y) * scaleAmplitude.y;

            target.anchoredPosition = _baseAnchored + new Vector2(offsetX, offsetY);
            target.localEulerAngles = new Vector3(0f, 0f, _baseRotation + rot);
            target.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, 1f);
        }
    }
}
