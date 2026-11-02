using UnityEngine;

namespace VRComfortStudy
{
    public sealed class StudyTarget : MonoBehaviour
    {
        [SerializeField] private string targetId;
        private TargetManager owner;
        public string TargetId => string.IsNullOrWhiteSpace(targetId) ? gameObject.name : targetId;
        public void SetOwner(TargetManager value) => owner = value;
        // Bind XRSimpleInteractable.selectEntered to this zero-argument method in the Inspector.
        public void Select() { if (owner != null) owner.SelectTarget(this, "XR_SELECT"); }
    }
}
