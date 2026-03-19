// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Quality;

namespace Snake.Game
{
    /// <summary>
    /// Visual renderer for the Snake grid.
    /// Only creates GameObjects for the snake body, food, floor, and frame walls.
    /// Glow system: head point light + food point light (pulsing) + HDR emission.
    /// </summary>
    public class SnakeBoardRenderer : MonoBehaviour, IQualityResponsive
    {
        private SnakeGrid _grid;

        // Snake segment cubes — dynamically pooled (only head + body)
        private readonly List<GameObject> _snakeCubePool = new();
        private readonly List<Renderer> _snakeCubeRenderers = new();

        // Food — single cube
        private GameObject _foodCube;
        private Renderer _foodRenderer;

        // Floor — single plane replacing hundreds of empty-cell cubes
        private GameObject _floorPlane;

        // Frame/border
        private GameObject _frameObject;

        // Dirty flag
        private bool _dirty = true;

        // Cell size in world units
        public const float CellSize = 0.5f;

        // Colors
        public static readonly Color SnakeHeadColor = new Color(0f, 1f, 0.4f);     // bright green
        public static readonly Color SnakeBodyColor = new Color(0f, 0.7f, 0.3f);    // green
        public static readonly Color FoodColor      = new Color(1f, 0.2f, 0.2f);    // red
        private static readonly Color FrameColor     = new Color(0.3f, 0.3f, 0.4f);

        // ── Glow system ──────────────────────────────────────────
        private Light _headLight;
        private const float HeadLightBaseIntensity = 0.3f;
        private const float HeadLightDecay = 3f;

        private Light _foodLight;
        private const float FoodLightBaseIntensity = 0.6f;
        private const float FoodLightRange = 2.5f;
        private float _foodPulsePhase;

        // Track flashed renderers for decay back to base color
        private readonly List<(Renderer renderer, Color baseColor)> _flashedRenderers = new();

        public void Initialize(SnakeGrid grid)
        {
            _grid = grid;

            // Single floor plane replaces per-cell empty cubes
            BuildFloor();

            // Food cube — single object
            _foodCube = CreateCube("Food");
            _foodRenderer = _foodCube.GetComponent<Renderer>();
            _foodCube.SetActive(false);

            // Pre-allocate snake cube pool for initial body size
            for (int i = 0; i < _grid.Body.Count + 4; i++)
                GrowPool();

            BuildFrame();

            _grid.OnGridChanged += () => _dirty = true;
            QualityBridge.Register(this);
        }

        private void OnDisable() => QualityBridge.Unregister(this);
        public void OnQualityChanged(QualityTier tier) => _dirty = true;

        private void LateUpdate()
        {
            DecayHeadLight();
            DecayFoodLight();
            DecayFlashedRenderers();

            if (!_dirty) return;
            _dirty = false;
            RenderGrid();
        }

        public void MarkDirty() => _dirty = true;

        // ═══════════════════════════════════════════════════════════════
        // GRID RENDERING
        // ═══════════════════════════════════════════════════════════════

        private void RenderGrid()
        {
            // Ensure pool is big enough for current snake length
            while (_snakeCubePool.Count < _grid.Body.Count)
                GrowPool();

            // Render snake segments (head at index 0)
            for (int i = 0; i < _grid.Body.Count; i++)
            {
                var (row, col) = _grid.Body[i];
                var go = _snakeCubePool[i];
                go.SetActive(true);

                bool isHead = (i == 0);
                Color color = isHead ? SnakeHeadColor : SnakeBodyColor;
                float heightScale = isHead ? CellSize * 0.95f : CellSize * 0.85f;

                go.transform.localPosition = CellToWorld(row, col, heightScale * 0.5f);
                go.transform.localScale = new Vector3(CellSize * 0.9f, heightScale, CellSize * 0.9f);
                SetCellColor(go, color);
            }

            // Hide unused pool cubes
            for (int i = _grid.Body.Count; i < _snakeCubePool.Count; i++)
                _snakeCubePool[i].SetActive(false);

            // Render food
            if (_foodCube != null && !_grid.IsDead)
            {
                var (fr, fc) = _grid.FoodPos;
                float foodScale = CellSize * 0.7f;
                _foodCube.transform.localPosition = CellToWorld(fr, fc, foodScale * 0.5f);
                _foodCube.transform.localScale = new Vector3(CellSize * 0.9f, foodScale, CellSize * 0.9f);

                // Food always renders with HDR emission for bloom glow
                Color foodHDR = new Color(FoodColor.r * 2f, FoodColor.g * 2f, FoodColor.b * 2f);
                SetHDRColorMat(_foodRenderer.material, foodHDR);
                _foodCube.SetActive(true);
            }
            else if (_foodCube != null)
            {
                _foodCube.SetActive(false);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CELL HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Vector3 CellToWorld(int row, int col, float yOffset = 0f)
        {
            return new Vector3(
                col * CellSize + CellSize * 0.5f,
                yOffset,
                row * CellSize + CellSize * 0.5f);
        }

        private GameObject CreateCube(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (CellSize * 0.9f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private void GrowPool()
        {
            int idx = _snakeCubePool.Count;
            var go = CreateCube($"Snake_{idx}");
            go.SetActive(false);
            _snakeCubePool.Add(go);
            _snakeCubeRenderers.Add(go.GetComponent<Renderer>());
        }

        private void BuildFloor()
        {
            _floorPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floorPlane.name = "Floor";
            _floorPlane.transform.SetParent(transform, false);
            float boardW = _grid.Width * CellSize;
            float boardH = _grid.Height * CellSize;
            _floorPlane.transform.localPosition = new Vector3(boardW * 0.5f, -0.01f, boardH * 0.5f);
            _floorPlane.transform.localScale = new Vector3(boardW, 0.02f, boardH);
            var col = _floorPlane.GetComponent<Collider>();
            if (col != null) Destroy(col);
            SetCellColor(_floorPlane, new Color(0.03f, 0.03f, 0.06f));
        }

        private void SetCellColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = renderer.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
        }

        // ═══════════════════════════════════════════════════════════════
        // GLOW / FLASH API — called by SnakeBootstrap event wiring
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Create a point light on the snake head cell. Call once after build.</summary>
        public void CreateHeadLight()
        {
            if (_headLight != null) return;
            var lightGO = new GameObject("HeadGlow");
            lightGO.transform.SetParent(transform, false);
            _headLight = lightGO.AddComponent<Light>();
            _headLight.type = LightType.Point;
            _headLight.range = 3f;
            _headLight.intensity = HeadLightBaseIntensity;
            _headLight.color = SnakeHeadColor;
            _headLight.shadows = LightShadows.None;
        }

        /// <summary>Create a point light on the food. Pulses gently in LateUpdate.</summary>
        public void CreateFoodLight()
        {
            if (_foodLight != null) return;
            var lightGO = new GameObject("FoodGlow");
            lightGO.transform.SetParent(transform, false);
            _foodLight = lightGO.AddComponent<Light>();
            _foodLight.type = LightType.Point;
            _foodLight.range = FoodLightRange;
            _foodLight.intensity = FoodLightBaseIntensity;
            _foodLight.color = FoodColor;
            _foodLight.shadows = LightShadows.None;
        }

        /// <summary>Flash the head light to a high intensity + color.</summary>
        public void FlashHeadLight(float intensity, Color color)
        {
            if (_headLight == null) return;
            _headLight.intensity = intensity;
            _headLight.color = color;
            _headLight.range = 3f + intensity * 0.4f;
        }

        /// <summary>Flash the food light to a high intensity.</summary>
        public void FlashFoodLight(float intensity, Color color)
        {
            if (_foodLight == null) return;
            _foodLight.intensity = intensity;
            _foodLight.color = color;
            _foodLight.range = FoodLightRange + intensity * 0.3f;
        }

        /// <summary>Flash the head cell material to HDR bloom burst.</summary>
        public void FlashHeadColor(float boostMultiplier)
        {
            if (_grid == null || _grid.Body.Count == 0) return;
            if (_snakeCubePool.Count == 0) return;
            var go = _snakeCubePool[0]; // head is always index 0
            if (go == null) return;
            Color boosted = new Color(SnakeHeadColor.r * boostMultiplier,
                                       SnakeHeadColor.g * boostMultiplier,
                                       SnakeHeadColor.b * boostMultiplier);
            SetHDRColor(go, boosted);
        }

        /// <summary>Flash the food cube + food light when eaten.</summary>
        public void FlashFoodEaten()
        {
            if (_foodCube == null || _foodRenderer == null) return;

            // Flash food cube HDR
            Color hdr = new Color(FoodColor.r * 6f, FoodColor.g * 6f, FoodColor.b * 6f);
            FlashRenderer(_foodCube, hdr, FoodColor);

            // Flash food light
            FlashFoodLight(3f, FoodColor);
        }

        // ═══════════════════════════════════════════════════════════════
        // DECAY — runs every LateUpdate
        // ═══════════════════════════════════════════════════════════════

        private void DecayHeadLight()
        {
            if (_headLight == null) return;
            float decay = Mathf.Clamp01(HeadLightDecay * Time.unscaledDeltaTime);
            _headLight.intensity = Mathf.Lerp(_headLight.intensity, HeadLightBaseIntensity, decay);
            _headLight.color = Color.Lerp(_headLight.color, SnakeHeadColor, decay);
            _headLight.range = Mathf.Lerp(_headLight.range, 3f, decay);

            // Move light to head position
            if (_grid != null && _grid.Body.Count > 0)
            {
                var head = _grid.Body[0];
                _headLight.transform.localPosition = CellToWorld(head.row, head.col, CellSize);
            }
        }

        private void DecayFoodLight()
        {
            if (_foodLight == null) return;

            // Gentle pulse
            _foodPulsePhase += Time.unscaledDeltaTime * 3f;
            float pulse = FoodLightBaseIntensity + Mathf.Sin(_foodPulsePhase) * 0.15f;

            // Decay flash intensity back toward pulse baseline
            float decay = Mathf.Clamp01(HeadLightDecay * Time.unscaledDeltaTime);
            _foodLight.intensity = Mathf.Lerp(_foodLight.intensity, pulse, decay);
            _foodLight.color = Color.Lerp(_foodLight.color, FoodColor, decay);
            _foodLight.range = Mathf.Lerp(_foodLight.range, FoodLightRange, decay);

            // Move light to food position
            if (_grid != null)
            {
                var (fr, fc) = _grid.FoodPos;
                _foodLight.transform.localPosition = CellToWorld(fr, fc, CellSize);
            }
        }

        private void DecayFlashedRenderers()
        {
            float decay = Mathf.Clamp01(HeadLightDecay * Time.unscaledDeltaTime);
            for (int i = _flashedRenderers.Count - 1; i >= 0; i--)
            {
                var (fr, baseCol) = _flashedRenderers[i];
                if (fr == null) { _flashedRenderers.RemoveAt(i); continue; }
                var mat = fr.material;
                Color current = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                Color next = Color.Lerp(current, baseCol, decay);
                SetHDRColorMat(mat, next);
                if (Mathf.Abs(next.r - baseCol.r) + Mathf.Abs(next.g - baseCol.g) + Mathf.Abs(next.b - baseCol.b) < 0.03f)
                {
                    SetHDRColorMat(mat, baseCol);
                    _flashedRenderers.RemoveAt(i);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HDR HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void FlashRenderer(GameObject go, Color hdrColor, Color baseColor)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            int idx = _flashedRenderers.FindIndex(e => e.renderer == r);
            Color origColor = baseColor;
            if (idx >= 0)
            {
                origColor = _flashedRenderers[idx].baseColor;
                _flashedRenderers.RemoveAt(idx);
            }
            _flashedRenderers.Add((r, origColor));
            SetHDRColorMat(r.material, hdrColor);
        }

        private static void SetHDRColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            SetHDRColorMat(renderer.material, color);
        }

        private static void SetHDRColorMat(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FRAME / BORDER
        // ═══════════════════════════════════════════════════════════════

        private void BuildFrame()
        {
            if (_frameObject != null) Destroy(_frameObject);

            _frameObject = new GameObject("Frame");
            _frameObject.transform.SetParent(transform, false);

            float boardW = _grid.Width * CellSize;
            float boardH = _grid.Height * CellSize;
            float wallHeight = CellSize * 0.6f;
            float thickness = CellSize * 0.15f;

            // Left wall
            CreateWall("Left",
                new Vector3(-thickness * 0.5f, wallHeight * 0.5f, boardH * 0.5f),
                new Vector3(thickness, wallHeight, boardH + thickness * 2));

            // Right wall
            CreateWall("Right",
                new Vector3(boardW + thickness * 0.5f, wallHeight * 0.5f, boardH * 0.5f),
                new Vector3(thickness, wallHeight, boardH + thickness * 2));

            // Bottom wall (near camera)
            CreateWall("Bottom",
                new Vector3(boardW * 0.5f, wallHeight * 0.5f, -thickness * 0.5f),
                new Vector3(boardW + thickness * 2, wallHeight, thickness));

            // Top wall (far from camera)
            CreateWall("Top",
                new Vector3(boardW * 0.5f, wallHeight * 0.5f, boardH + thickness * 0.5f),
                new Vector3(boardW + thickness * 2, wallHeight, thickness));
        }

        private void CreateWall(string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_frameObject.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            SetCellColor(go, FrameColor);
        }
    }
}
