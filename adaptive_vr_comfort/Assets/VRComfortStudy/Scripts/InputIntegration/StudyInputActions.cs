#if VR_COMFORT_INPUT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRComfortStudy
{
    public sealed class StudyInputActions : MonoBehaviour
    {
        [SerializeField] private ExperimentManager experiment;
        [SerializeField] private InputActionReference stopAction;
        [SerializeField] private InputActionReference ratingAction;
        [SerializeField] private InputActionReference lessVignetteAction;
        [SerializeField] private bool simulatorKeyboardShortcuts = true;
        private readonly HashSet<InputAction> enabledHere = new HashSet<InputAction>();

        private void OnEnable()
        {
            Bind(stopAction, OnStop);
            Bind(ratingAction, OnRating);
            Bind(lessVignetteAction, OnLess);
        }

        private void Bind(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
        {
            if (reference == null || reference.action == null) return;
            InputAction action = reference.action;
            action.performed += handler;
            if (!action.enabled) { action.Enable(); enabledHere.Add(action); }
        }

        private void Unbind(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
        { if (reference != null && reference.action != null) reference.action.performed -= handler; }

        private void OnDisable()
        {
            Unbind(stopAction, OnStop);
            Unbind(ratingAction, OnRating);
            Unbind(lessVignetteAction, OnLess);
            foreach (InputAction action in enabledHere) action.Disable();
            enabledHere.Clear();
        }

        private void OnStop(InputAction.CallbackContext _) { if (experiment != null) experiment.StopTrial(); }
        private void OnRating(InputAction.CallbackContext _) { if (experiment != null) experiment.RequestRating(); }
        private void OnLess(InputAction.CallbackContext _) { if (experiment != null) experiment.RequestLessVignette(); }

        private void Update()
        {
            if (!simulatorKeyboardShortcuts || experiment == null || experiment.InputSource != StudyInputSource.Simulator) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) experiment.StopTrial();
            if (keyboard.rKey.wasPressedThisFrame) experiment.RequestRating();
        }
    }
}
#endif
