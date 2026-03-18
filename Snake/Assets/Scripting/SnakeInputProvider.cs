// Copyright CodeGamified 2025-2026
// MIT License — Snake
using UnityEngine;
using UnityEngine.InputSystem;

namespace Snake.Scripting
{
    /// <summary>
    /// Captures keyboard/gamepad input for Snake.
    /// Encodes as a single float: 0=none, 1=up, 2=right, 3=down, 4=left.
    /// Matches Direction enum +1 (0 reserved for "no input").
    /// </summary>
    public class SnakeInputProvider : MonoBehaviour
    {
        public static SnakeInputProvider Instance { get; private set; }

        public const float INPUT_NONE  = 0f;
        public const float INPUT_UP    = 1f;
        public const float INPUT_RIGHT = 2f;
        public const float INPUT_DOWN  = 3f;
        public const float INPUT_LEFT  = 4f;

        public float CurrentInput { get; private set; }

        private InputAction _upAction;
        private InputAction _rightAction;
        private InputAction _downAction;
        private InputAction _leftAction;

        private void Awake()
        {
            Instance = this;

            _upAction = new InputAction("Up", InputActionType.Button);
            _upAction.AddBinding("<Keyboard>/upArrow");
            _upAction.AddBinding("<Keyboard>/w");
            _upAction.Enable();

            _rightAction = new InputAction("Right", InputActionType.Button);
            _rightAction.AddBinding("<Keyboard>/rightArrow");
            _rightAction.AddBinding("<Keyboard>/d");
            _rightAction.Enable();

            _downAction = new InputAction("Down", InputActionType.Button);
            _downAction.AddBinding("<Keyboard>/downArrow");
            _downAction.AddBinding("<Keyboard>/s");
            _downAction.Enable();

            _leftAction = new InputAction("Left", InputActionType.Button);
            _leftAction.AddBinding("<Keyboard>/leftArrow");
            _leftAction.AddBinding("<Keyboard>/a");
            _leftAction.Enable();
        }

        private void Update()
        {
            if (_upAction.WasPressedThisFrame())
                CurrentInput = INPUT_UP;
            else if (_rightAction.WasPressedThisFrame())
                CurrentInput = INPUT_RIGHT;
            else if (_downAction.WasPressedThisFrame())
                CurrentInput = INPUT_DOWN;
            else if (_leftAction.WasPressedThisFrame())
                CurrentInput = INPUT_LEFT;
            else
                CurrentInput = INPUT_NONE;
        }

        private void OnDestroy()
        {
            _upAction?.Disable(); _upAction?.Dispose();
            _rightAction?.Disable(); _rightAction?.Dispose();
            _downAction?.Disable(); _downAction?.Dispose();
            _leftAction?.Disable(); _leftAction?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
