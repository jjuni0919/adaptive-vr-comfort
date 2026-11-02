#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRComfortStudy.Editor
{
    public static class StudySetupMenu
    {
        [MenuItem("Tools/VR Comfort Study/Add Study Components To Current Scene")]
        public static void AddStudyComponents()
        {
            ExperimentManager existing = UnityEngine.Object.FindFirstObjectByType<ExperimentManager>();
            if (existing != null) { Selection.activeObject = existing.gameObject; return; }
            var services = new GameObject("StudyServices");
            Undo.RegisterCreatedObjectUndo(services, "Add VR comfort study");
            var logger = services.AddComponent<SessionLogger>();
            var motion = services.AddComponent<MotionFeatureCollector>();
            var vignette = services.AddComponent<VignetteAdapter>();
            var rating = services.AddComponent<RatingUI>();
            var targets = services.AddComponent<TargetManager>();
            var experiment = services.AddComponent<ExperimentManager>();
            var panel = services.AddComponent<StudyDebugPanel>();
            SetReference(panel, "experiment", experiment);
            SetReference(motion, "experiment", experiment);
            SetReference(targets, "experiment", experiment);
            SetReference(experiment, "logger", logger);
            SetReference(experiment, "features", motion);
            SetReference(experiment, "vignette", vignette);
            SetReference(experiment, "ratingUI", rating);
            SetReference(experiment, "targets", targets);
            Type inputType = Type.GetType("VRComfortStudy.StudyInputActions, VRComfortStudy.Input");
            if (inputType != null)
            {
                Component input = services.AddComponent(inputType);
                SetReference(input, "experiment", experiment);
            }
            Camera camera = Camera.main;
            if (camera != null)
            {
                SetReference(motion, "head", camera.transform);
                foreach (Component parent in camera.GetComponentsInParent<Component>(true))
                    if (parent != null && parent.GetType().FullName == "Unity.XR.CoreUtils.XROrigin")
                    {
                        SetReference(motion, "artificialMotionRoot", parent.transform);
                        var providers = new List<Behaviour>();
                        foreach (Behaviour item in parent.GetComponentsInChildren<Behaviour>(true))
                            if (item != null && IsLocomotionProvider(item.GetType())) providers.Add(item);
                        var setup = new SerializedObject(experiment);
                        SerializedProperty list = setup.FindProperty("movementProviders");
                        list.arraySize = providers.Count;
                        for (int i = 0; i < providers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = providers[i];
                        setup.ApplyModifiedPropertiesWithoutUndo();
                        break;
                    }
            }
            Selection.activeObject = services;
            EditorSceneManager.MarkSceneDirty(services.scene);
            Debug.Log("StudyServices added. Assign motion root/head, movement providers, targets, and the XRI vignette output. See the setup MD.");
        }

        private static bool IsLocomotionProvider(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
                if (current.FullName == "UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider") return true;
            return false;
        }

        [MenuItem("Tools/VR Comfort Study/Create Three Demo Targets")]
        public static void CreateDemoTargets()
        {
            ExperimentManager experiment = UnityEngine.Object.FindFirstObjectByType<ExperimentManager>();
            if (experiment == null) { Debug.LogError("Add Study Components first."); return; }
            TargetManager manager = experiment.GetComponent<TargetManager>();
            if (manager == null) return;
            var serialized = new SerializedObject(manager);
            SerializedProperty list = serialized.FindProperty("targets");
            if (list.arraySize > 0) { Debug.LogWarning("Targets already assigned; keeping your scene unchanged."); return; }
            var root = new GameObject("StudyDemoTargets");
            Undo.RegisterCreatedObjectUndo(root, "Create study targets");
            Camera camera = Camera.main;
            Vector3 origin = camera != null ? camera.transform.position : new Vector3(0, 1.4f, 0);
            Vector3 forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Type interactable = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit");
            list.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name = "StudyTarget_" + (i + 1);
                obj.transform.SetParent(root.transform);
                obj.transform.position = origin + forward * (3 + i) + right * (i - 1);
                obj.transform.localScale = Vector3.one * 0.3f;
                StudyTarget target = obj.AddComponent<StudyTarget>();
                list.GetArrayElementAtIndex(i).objectReferenceValue = target;
                if (interactable != null)
                {
                    Component instance = obj.AddComponent(interactable);
                    var signal = interactable.GetProperty("selectEntered")?.GetValue(instance) as UnityEngine.Events.UnityEventBase;
                    if (signal != null) UnityEventTools.AddVoidPersistentListener(signal, target.Select);
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/VR Comfort Study/Check XRI Vignette Bridge")]
        public static void CheckXriBridge()
        {
            Type controller = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController, Unity.XR.Interaction.Toolkit");
            if (controller == null) { Debug.LogError("Install XR Interaction Toolkit 3.x before enabling this bridge."); return; }
            Type bridge = Type.GetType("VRComfortStudy.XriVignetteOutput, VRComfortStudy.XRI");
            if (bridge == null) Debug.LogError("Bridge not compiled. Keep the Integrations asmdef and use XR Interaction Toolkit 3.x.");
            else Debug.Log("XRI vignette bridge is available. Add XriVignetteOutput to TunnelingVignette and assign its Adapter and Controller.");
        }
    }
}
#endif
