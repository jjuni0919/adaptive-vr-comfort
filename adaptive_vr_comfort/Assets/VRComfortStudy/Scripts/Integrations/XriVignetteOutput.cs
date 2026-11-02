// Enable VR_COMFORT_XRI3 only when XR Interaction Toolkit 3.x is installed.
#if VR_COMFORT_XRI3
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace VRComfortStudy
{
    [DefaultExecutionOrder(400)]
    public sealed class XriVignetteOutput : MonoBehaviour, ITunnelingVignetteProvider
    {
        [SerializeField] private VignetteAdapter adapter;
        [SerializeField] private TunnelingVignetteController controller;
        private VignetteParameters parameters;
        private bool active;
        public VignetteParameters vignetteParameters => parameters;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<TunnelingVignetteController>();
            parameters = new VignetteParameters {
                apertureSize = 1, featheringEffect = 0.4f,
                easeInTime = 0, easeOutTime = 0, easeOutDelayTime = 0, easeInTimeLock = false
            };
            if (controller != null && controller.locomotionVignetteProviders.Count > 0)
                Debug.LogWarning("Clear the controller's Locomotion Vignette Providers for exclusive study control.", this);
        }

        private void Update()
        {
            if (adapter == null || controller == null || parameters == null) return;
            parameters.apertureSize = adapter.isActiveAndEnabled ? adapter.Aperture : 1;
            if (parameters.apertureSize < 0.9999f)
            {
                controller.BeginTunnelingVignette(this);
                active = true;
            }
            else if (active)
            {
                controller.EndTunnelingVignette(this);
                active = false;
            }
        }

        private void OnDisable()
        {
            if (controller != null && active) controller.EndTunnelingVignette(this);
            active = false;
        }
    }
}
#endif
