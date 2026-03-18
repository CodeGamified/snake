// Copyright CodeGamified 2025-2026
// MIT License — Snake
using UnityEngine;
using CodeGamified.Quality;

namespace Snake.Game
{
    /// <summary>
    /// Visual renderer for the Snake grid — 3D cube cells.
    /// Renders grid cells (empty, food, snake head, snake body) each frame.
    /// Uses a flat object pool — all cells pre-created and toggled.
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
        private static readonly Color SnakeHeadColor = new Color(0f, 1f, 0.4f);     // bright green
        private static readonly Color SnakeBodyColor = new Color(0f, 0.7f, 0.3f);    // green
        private static readonly Color FoodColor      = new Color(1f, 0.2f, 0.2f);    // red
        private static readonly Color EmptyColor     = new Color(0.03f, 0.03f, 0.06f); // near-black
        private static readonly Color FrameColor     = new Color(0.3f, 0.3f, 0.4f);

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
