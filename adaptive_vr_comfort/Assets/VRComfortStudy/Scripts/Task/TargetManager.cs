using UnityEngine;

namespace VRComfortStudy
{
    public sealed class TargetManager : MonoBehaviour
    {
        [SerializeField] private ExperimentManager experiment;
        [SerializeField] private StudyTarget[] targets = new StudyTarget[0];
        [SerializeField] private bool repeatSequence = true;
        public StringEvent onTargetChanged = new StringEvent();
        private int index;
        private double shownAt;
        public int SelectedCount { get; private set; }
        public StudyTarget Current => index >= 0 && index < targets.Length ? targets[index] : null;

        public void Begin(ExperimentManager owner)
        {
            experiment = owner;
            SelectedCount = 0;
            index = 0;
            foreach (StudyTarget target in targets)
                if (target != null) { target.SetOwner(this); target.gameObject.SetActive(false); }
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (Current == null) { onTargetChanged.Invoke("과제 표적 없음"); return; }
            Current.gameObject.SetActive(true);
            shownAt = experiment.ActiveTime;
            experiment.LogEvent("TARGET_SHOWN", Current.TargetId);
            onTargetChanged.Invoke(Current.TargetId);
        }

        public void SelectTarget(StudyTarget target, string source)
        {
            if (experiment == null || experiment.State != StudyState.Running || target == null) return;
            if (target != Current)
            {
                experiment.LogEvent("TARGET_ERROR", target.TargetId);
                return;
            }
            experiment.LogEvent("TARGET_SELECTED", CsvFormat.Row(target.TargetId, experiment.ActiveTime - shownAt, source));
            SelectedCount++;
            target.gameObject.SetActive(false);
            index++;
            if (index >= targets.Length && repeatSequence) index = 0;
            ShowCurrent();
        }

        public void SelectCurrentForDebug()
        {
            if (experiment != null && experiment.InputSource == StudyInputSource.Simulator)
                SelectTarget(Current, "DEBUG_SELECT_CURRENT");
        }

        public void End()
        {
            foreach (StudyTarget target in targets) if (target != null) target.gameObject.SetActive(false);
        }
    }
}
