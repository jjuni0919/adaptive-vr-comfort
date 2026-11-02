using UnityEngine;

namespace VRComfortStudy
{
    /// <summary>Editor/development overlay: works without TMP, uGUI, Input System, or a headset.</summary>
    public sealed class StudyDebugPanel : MonoBehaviour
    {
        [SerializeField] private ExperimentManager experiment;
        [SerializeField] private bool visible = true;
        private Rect bounds = new Rect(12, 12, 430, 570);

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!visible || experiment == null) return;
            bounds = GUILayout.Window(GetInstanceID(), bounds, DrawPanel, "VR Comfort — DEBUG / SIMULATOR");
#endif
        }

        private void DrawPanel(int id)
        {
            GUILayout.Label("State: " + experiment.State);
            GUILayout.Label("Active: " + experiment.ActiveTime.ToString("F1") + " s / Wall: " + experiment.WallTime.ToString("F1") + " s");
            GUILayout.Label("Input source: " + experiment.InputSource);
            GUILayout.Label(experiment.LastStatus);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start")) { experiment.StartTrial(); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("STOP")) { experiment.StopTrial(); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("Reset")) { experiment.ResetTrial(); GUIUtility.ExitGUI(); }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Request rating now")) { experiment.RequestRating(); GUIUtility.ExitGUI(); }
            if (experiment.Targets != null && GUILayout.Button("Select current target (SIMULATOR ONLY)"))
                experiment.Targets.SelectCurrentForDebug();
            GUILayout.Label("Manual vignette (not an estimated sickness score)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("None")) experiment.SetManualVignetteStrength(0);
            if (GUILayout.Button("Weak")) experiment.SetManualVignetteStrength(0.25f);
            if (GUILayout.Button("Medium")) experiment.SetManualVignetteStrength(0.5f);
            if (GUILayout.Button("Strong")) experiment.SetManualVignetteStrength(1);
            GUILayout.EndHorizontal();
            if (experiment.Vignette != null)
                GUILayout.Label("Command strength: " + experiment.Vignette.CommandStrength.ToString("F2") +
                    " / Aperture command: " + experiment.Vignette.Aperture.ToString("F2"));
            if (experiment.State == StudyState.Rating && experiment.RatingPanel != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("직전 현재 멀미 불편: 0 없음 / 10 매우 심함");
                GUILayout.BeginHorizontal();
                for (int i = 0; i <= 10; i++)
                    if (GUILayout.Button(i.ToString(), GUILayout.MinWidth(28))) experiment.RatingPanel.SelectScore(i);
                GUILayout.EndHorizontal();
                GUILayout.Label("Selected: " + experiment.RatingPanel.SelectedScore);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Confirm")) { experiment.RatingPanel.Confirm(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Skip (missing, not zero)")) { experiment.RatingPanel.Skip(); GUIUtility.ExitGUI(); }
                GUILayout.EndHorizontal();
                FeatureSnapshot p = experiment.PendingRating;
                if (p != null) GUILayout.Label("Snapshot: " + p.snapshot_id + " / Valid: " + p.window_ready);
            }
            GUILayout.Space(8);
            if (experiment.Logger != null) GUILayout.Label("Logs: " + experiment.Logger.OutputDirectory);
            GUI.DragWindow();
        }
    }
}
