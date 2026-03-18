// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Quality;

namespace Snake.Game
{
    /// <summary>
    /// Visual renderer for the Snake grid — 3D cube cells.
    /// Renders grid cells (empty, food, snake head, snake body) each frame.
    /// Uses a flat object pool — all cells pre-created and toggled.
    /// Glow system: head point light + HDR emission flashing.
    /// </summary>
    public class SnakeBoardRenderer : MonoBehaviour, IQualityResponsive
    {
        private SnakeGrid _grid;

        // Cell GameObjects — flat pool [row * Width + col]
        private GameObject[] _cellObjects;

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
        private static readonly Color EmptyColor     = new Color(0.03f, 0.03f, 0.06f); // near-black
        private static readonly Color FrameColor     = new Color(0.3f, 0.3f, 0.4f);

        // ── Glow system ──────────────────────────────────────────
        private Light _headLight;
        private const float HeadLightBaseIntensity = 0.3f;
        private const float HeadLightDecay = 3f;

        // Track flashed renderers for decay back to base color
        private readonly List<(Renderer renderer, Color baseColor)> _flashedRenderers = new();

        // Food eat glow coroutines
        private readonly Dictionary<GameObject, Coroutine> _cellGlowCoroutines = new();

        public void Initialize(SnakeGrid grid)
        {
            _grid = grid;

            int total = _grid.Height * _grid.Width;
            _cellObjects = new GameObject[total];

            for (int r = 0; r < _grid.Height; r++)
            {
                for (int c = 0; c < _grid.Width; c++)
                {
                    int idx = r * _grid.Width + c;
                    var go = CreateCellObject($"Cell_{r}_{c}");
                    go.transform.localPosition = CellToWorld(r, c);
                    _cellObjects[idx] = go;
                }
            }

            BuildFrame();

            _grid.OnGridChanged += () => _dirty = true;
            QualityBridge.Register(this);
        }

        private void OnDisable() => QualityBridge.Unregister(this);
        public void OnQualityChanged(QualityTier tier) => _dirty = true;

        private void LateUpdate()
        {
            DecayHeadLight();
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
            for (int r = 0; r < _grid.Height; r++)
            {
                for (int c = 0; c < _grid.Width; c++)
                {
                    int idx = r * _grid.Width + c;
                    int val = _grid.Grid[r, c];
                    var go = _cellObjects[idx];

                    Color color = val switch
                    {
                        1 => FoodColor,
                        2 => SnakeHeadColor,
                        3 => SnakeBodyColor,
                        _ => EmptyColor
                    };

                    float scale = val switch
                    {
                        1 => CellSize * 0.7f,  // food slightly smaller
                        2 => CellSize * 0.95f,  // head prominent
                        3 => CellSize * 0.85f,  // body segments
                        _ => CellSize * 0.1f     // empty = thin floor tile
                    };

                    go.transform.localScale = new Vector3(
                        val > 0 ? CellSize * 0.9f : CellSize * 0.95f,
                        scale,
                        val > 0 ? CellSize * 0.9f : CellSize * 0.95f);

                    go.transform.localPosition = CellToWorld(r, c, val > 0 ? scale * 0.5f : 0.01f);
                    SetCellColor(go, color);
                    go.SetActive(true);
                }
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

        private GameObject CreateCellObject(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (CellSize * 0.9f);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
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

        /// <summary>Flash the head light to a high intensity + color.</summary>
        public void FlashHeadLight(float intensity, Color color)
        {
            if (_headLight == null) return;
            _headLight.intensity = intensity;
            _headLight.color = color;
            _headLight.range = 3f + intensity * 0.4f;
        }

        /// <summary>Flash the head cell material to HDR bloom burst.</summary>
        public void FlashHeadColor(float boostMultiplier)
        {
            if (_grid == null || _grid.Body.Count == 0) return;
            var head = _grid.Body[0];
            int idx = head.row * _grid.Width + head.col;
            if (idx < 0 || idx >= _cellObjects.Length) return;
            var go = _cellObjects[idx];
            if (go == null) return;
            Color boosted = new Color(SnakeHeadColor.r * boostMultiplier,
                                       SnakeHeadColor.g * boostMultiplier,
                                       SnakeHeadColor.b * boostMultiplier);
            SetHDRColor(go, boosted);
        }

        /// <summary>Flash a food cell with glow when eaten.</summary>
        public void FlashFoodEaten(int row, int col)
        {
            int idx = row * _grid.Width + col;
            if (idx < 0 || idx >= _cellObjects.Length) return;
            var go = _cellObjects[idx];
            if (go == null) return;

            if (_cellGlowCoroutines.TryGetValue(go, out var existing))
            {
                if (existing != null) StopCoroutine(existing);
                _cellGlowCoroutines.Remove(go);
            }

            var c = StartCoroutine(FoodEatGlow(go));
            _cellGlowCoroutines[go] = c;
        }

        /// <summary>Flash any cell renderer with HDR color, then decay back.</summary>
        public void FlashCellGlow(int row, int col, Color hdrColor, Color baseColor)
        {
            int idx = row * _grid.Width + col;
            if (idx < 0 || idx >= _cellObjects.Length) return;
            var go = _cellObjects[idx];
            if (go == null) return;
            FlashRenderer(go, hdrColor, baseColor);
        }

        private IEnumerator FoodEatGlow(GameObject go)
        {
            Color hdr = new Color(FoodColor.r * 5f, FoodColor.g * 5f, FoodColor.b * 5f);
            SetHDRColor(go, hdr);

            float elapsed = 0f;
            const float glowDuration = 0.2f;
            while (elapsed < glowDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / glowDuration;
                Color faded = Color.Lerp(hdr, EmptyColor, t);
                SetHDRColor(go, faded);
                yield return null;
            }

            SetHDRColor(go, EmptyColor);
            _cellGlowCoroutines.Remove(go);
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
