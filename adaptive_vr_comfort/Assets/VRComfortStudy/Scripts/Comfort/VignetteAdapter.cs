using UnityEngine;

namespace VRComfortStudy
{
    public sealed class VignetteAdapter : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float minimumAperture = 0.45f;
        [SerializeField, Min(0)] private float transitionSeconds = 0.5f;
        [SerializeField, Range(0, 1)] private float targetStrength;
        [Tooltip("Connect this to a rendering bridge, not an XR camera FOV setting.")]
        public FloatEvent onApertureChanged = new FloatEvent();
        public FloatEvent onStrengthChanged = new FloatEvent();
        public float CommandStrength { get; private set; }
        public float Aperture => Mathf.Lerp(1, minimumAperture, CommandStrength);

        public void SetTargetStrength(float value) => targetStrength = Mathf.Clamp01(value);
        public void Clear() => SetTargetStrength(0);
        public void SetWeak() => SetTargetStrength(0.25f);
        public void SetMedium() => SetTargetStrength(0.5f);
        public void SetStrong() => SetTargetStrength(1);

        private void Update()
        {
            CommandStrength = transitionSeconds <= 0 ? targetStrength :
                Mathf.MoveTowards(CommandStrength, targetStrength, Time.unscaledDeltaTime / transitionSeconds);
            onApertureChanged.Invoke(Aperture);
            onStrengthChanged.Invoke(CommandStrength);
        }

        private void OnDisable()
        {
            CommandStrength = 0;
            onApertureChanged.Invoke(1);
            onStrengthChanged.Invoke(0);
        }
    }
}
