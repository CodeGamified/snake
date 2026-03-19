// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CodeGamified.Camera;
using CodeGamified.Procedural;
using CodeGamified.Time;
using CodeGamified.Settings;
using CodeGamified.Quality;
using CodeGamified.Bootstrap;
using Snake.Game;
using Snake.Scripting;
using Snake.UI;

namespace Snake.Core
{
    /// <summary>
    /// Bootstrap for Snake — code-controlled grid crawler.
    ///
    /// Architecture (same pattern as Pong / Tetris):
    ///   - Instantiate managers → wire cross-references → configure scene
    ///   - .engine submodule gives us TUI + Code Execution for free
    ///   - Players don't press arrow keys — they WRITE CODE to steer the snake
    ///   - "Unit test" your navigation AI by watching it play at 100x speed
    ///
    /// Attach to a GameObject. Press Play → Snake appears.
    /// </summary>
    public class SnakeBootstrap : GameBootstrap, IQualityResponsive
    {
        protected override string LogTag => "SNAKE";

        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Grid")]
        [Tooltip("Grid width in cells")]
        public int gridWidth = 20;

        [Tooltip("Grid height in cells")]
        public int gridHeight = 20;

        [Header("Speed")]
        [Tooltip("Base step interval at start (sim-seconds per move)")]
        public float baseStepInterval = 0.15f;

        [Header("Match")]
        [Tooltip("Auto-restart after game over")]
        public bool autoRestart = true;

        [Tooltip("Delay before restarting (sim-seconds)")]
        public float restartDelay = 2f;

        [Header("Time")]
        [Tooltip("Enable time scale modulation for fast testing")]
        public bool enableTimeScale = true;

        [Header("Scripting")]
        [Tooltip("Enable code execution (.engine)")]
        public bool enableScripting = true;

        [Header("Camera")]
        public bool configureCamera = true;

        // =================================================================
        // RUNTIME REFERENCES
        // =================================================================

        private SnakeGrid _grid;
        private SnakeBoardRenderer _boardRenderer;
        private SnakeMatchManager _match;
        private SnakeProgram _playerProgram;

        // Trail
        private SnakeHeadTrail _headTrail;

        // TUI
        private SnakeTUIManager _tuiManager;

        // Camera
        private CameraAmbientMotion _cameraSway;

        // Post-processing
        private Bloom _bloom;
        private Volume _postProcessVolume;

        // =================================================================
        // UPDATE
        // =================================================================

        private void Update()
        {
            UpdateBloomScale();
        }

        private void UpdateBloomScale()
        {
            if (_bloom == null || !_bloom.active) return;
            var cam = Camera.main;
            if (cam == null) return;
            float dist = Vector3.Distance(cam.transform.position, BoardCenter());
            float defaultDist = 12f;
            float scale = Mathf.Clamp01(defaultDist / Mathf.Max(dist, 0.01f));
            _bloom.intensity.value = Mathf.Lerp(0.5f, 1.0f, scale);
        }

        // =================================================================
        // BOOTSTRAP
        // =================================================================

        private void Start()
        {
            Log("🐍 Snake Bootstrap starting...");

            SettingsBridge.Load();
            QualityBridge.SetTier((QualityTier)SettingsBridge.QualityLevel);
            QualityBridge.Register(this);
            Log($"Settings loaded (Quality={SettingsBridge.QualityLevel}, Font={SettingsBridge.FontSize}pt)");

            SetupSimulationTime();
            SetupCamera();
            CreateGrid();
            CreateMatchManager();
            CreateBoardRenderer();
            CreateHeadTrail();
            CreateInputProvider();

            if (enableScripting) CreatePlayerProgram();

            CreateTUI();
            WireEvents();
            StartCoroutine(RunBootSequence());
        }

        public void OnQualityChanged(QualityTier tier)
        {
            Log($"Quality changed → {tier}");
        }

        // =================================================================
        // SIMULATION TIME
        // =================================================================

        private void SetupSimulationTime()
        {
            EnsureSimulationTime<SnakeSimulationTime>();
        }

        // =================================================================
        // CAMERA — top-down-ish perspective view of the grid
        // =================================================================

        private Vector3 BoardCenter()
        {
            float boardW = gridWidth * SnakeBoardRenderer.CellSize;
            float boardH = gridHeight * SnakeBoardRenderer.CellSize;
            return new Vector3(boardW * 0.5f, 0f, boardH * 0.5f);
        }

        private void SetupCamera()
        {
            if (!configureCamera) return;

            var cam = EnsureCamera();

            cam.orthographic = false;
            cam.fieldOfView = 60f;
            var center = BoardCenter();
            // Position above and slightly in front for a 3/4 top-down view
            cam.transform.position = center + new Vector3(0f, 10f, -5f);
            cam.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // Ambient sway
            _cameraSway = cam.gameObject.AddComponent<CameraAmbientMotion>();
            _cameraSway.lookAtTarget = center;

            // Post-processing: bloom
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var volumeGO = new GameObject("PostProcessVolume");
            _postProcessVolume = volumeGO.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
            _postProcessVolume.priority = 1;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>();
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 0.8f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = 1.0f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.5f;
            _bloom.clamp.overrideState = true;
            _bloom.clamp.value = 20f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;
            _postProcessVolume.profile = profile;

            Log("Camera: perspective, FOV=60, top-down 3/4 view + sway + bloom");
        }

        // =================================================================
        // GRID
        // =================================================================

        private void CreateGrid()
        {
            var go = new GameObject("SnakeGrid");
            _grid = go.AddComponent<SnakeGrid>();
            _grid.Initialize(gridWidth, gridHeight);
            Log($"Created Grid ({gridWidth}×{gridHeight})");
        }

        // =================================================================
        // MATCH MANAGER
        // =================================================================

        private void CreateMatchManager()
        {
            var go = new GameObject("MatchManager");
            _match = go.AddComponent<SnakeMatchManager>();
            _match.Initialize(_grid, baseStepInterval);
            Log($"Created MatchManager (step={baseStepInterval}s)");
        }

        // =================================================================
        // BOARD RENDERER
        // =================================================================

        private void CreateBoardRenderer()
        {
            _boardRenderer = _grid.gameObject.AddComponent<SnakeBoardRenderer>();
            _boardRenderer.Initialize(_grid);
            _boardRenderer.CreateHeadLight();
            _boardRenderer.CreateFoodLight();
            Log("Created BoardRenderer (snake cubes, floor, walls + head/food glow)");
        }

        // =================================================================
        // HEAD TRAIL
        // =================================================================

        private void CreateHeadTrail()
        {
            var go = new GameObject("HeadTrail");
            _headTrail = go.AddComponent<SnakeHeadTrail>();
            _headTrail.Initialize(_grid, SnakeBoardRenderer.SnakeHeadColor);
            Log("Created HeadTrail (snake head trail)");
        }

        // =================================================================
        // INPUT PROVIDER
        // =================================================================

        private void CreateInputProvider()
        {
            var go = new GameObject("InputProvider");
            go.AddComponent<SnakeInputProvider>();
            Log("Created SnakeInputProvider (Unity Input System)");
        }

        // =================================================================
        // PLAYER SCRIPTING (.engine powered)
        // =================================================================

        private void CreatePlayerProgram()
        {
            var go = new GameObject("PlayerProgram");
            _playerProgram = go.AddComponent<SnakeProgram>();
            _playerProgram.Initialize(_match, _grid);
            Log("Created PlayerProgram (code-controlled snake AI)");
        }

        // =================================================================
        // TUI (.engine powered)
        // =================================================================

        private void CreateTUI()
        {
            var go = new GameObject("SnakeTUI");
            _tuiManager = go.AddComponent<SnakeTUIManager>();
            _tuiManager.Initialize(_match, _playerProgram);
            Log("Created TUI (left debugger + right status panel)");
        }

        // =================================================================
        // EVENT WIRING
        // =================================================================

        private void WireEvents()
        {
            if (SimulationTime.Instance != null)
            {
                SimulationTime.Instance.OnTimeScaleChanged += s => Log($"Time scale → {s:F0}x");
                SimulationTime.Instance.OnPausedChanged += p => Log(p ? "⏸ PAUSED" : "▶ RESUMED");
            }

            if (_match != null)
            {
                _match.OnMatchStarted += () =>
                {
                    Log("MATCH STARTED");
                    _boardRenderer?.MarkDirty();
                    _headTrail?.ClearLine();
                };

                _match.OnFoodEaten += score =>
                {
                    Log($"NOM! │ Score: {score} │ Length: {_grid.Body.Count}");
                    _boardRenderer?.MarkDirty();

                    // Flash food cube + food light
                    _boardRenderer?.FlashFoodEaten();

                    // Head glow on food eat — bright red burst
                    _boardRenderer?.FlashHeadLight(2.5f, SnakeBoardRenderer.FoodColor);
                    _boardRenderer?.FlashHeadColor(4f);

                    // Trail color changes with each eat
                    Color trailHDR = new Color(
                        SnakeBoardRenderer.FoodColor.r * 3f,
                        SnakeBoardRenderer.FoodColor.g * 3f,
                        SnakeBoardRenderer.FoodColor.b * 3f);
                    _headTrail?.SetColor(trailHDR);
                };

                _match.OnGameOver += () =>
                {
                    Log($"GAME OVER │ Score: {_match.Score} │ Length: {_grid.Body.Count} │ High: {_match.HighScore}");

                    // Death flash — big red
                    _boardRenderer?.FlashHeadLight(4f, Color.red);
                    _boardRenderer?.FlashHeadColor(6f);
                    _headTrail?.ClearLine();

                    if (autoRestart)
                        StartCoroutine(RestartAfterDelay());
                };

                _match.OnSnakeStepped += () =>
                {
                    _boardRenderer?.MarkDirty();

                    // Subtle head pulse each step
                    _boardRenderer?.FlashHeadLight(0.8f, SnakeBoardRenderer.SnakeHeadColor);
                    _boardRenderer?.FlashHeadColor(1.5f);
                };
            }
        }

        // =================================================================
        // BOOT SEQUENCE
        // =================================================================

        private IEnumerator RunBootSequence()
        {
            yield return null;
            yield return null;

            LogDivider();
            Log("🐍 SNAKE — Code Your Crawler");
            LogDivider();
            LogStatus("GRID", $"{gridWidth}×{gridHeight}");
            LogStatus("SPEED", $"{baseStepInterval}s/step");
            LogEnabled("SCRIPTING", enableScripting);
            LogEnabled("TIME SCALE", enableTimeScale);
            LogEnabled("AUTO RESTART", autoRestart);
            LogDivider();

            _match.StartMatch();
            Log("First match started — GO!");
        }

        private IEnumerator RestartAfterDelay()
        {
            float waited = 0f;
            while (waited < restartDelay)
            {
                if (SimulationTime.Instance != null && !SimulationTime.Instance.isPaused)
                    waited += Time.deltaTime * (SimulationTime.Instance?.timeScale ?? 1f);
                yield return null;
            }

            _match.StartMatch();
            _playerProgram?.ResetExecution();
            Log("Match restarted");
        }

        private void OnDestroy()
        {
            QualityBridge.Unregister(this);
        }
    }
}
