// Copyright CodeGamified 2025-2026
// MIT License — Snake
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Quality;
using CodeGamified.Time;
using Snake.Game;

namespace Snake.Game
{
    /// <summary>
    /// Procedural snake head trail — tracks the head cell position on XZ plane.
    ///  • Low/Med/High: ring buffer of spheres that fade out.
    ///  • Ultra: persistent LineRenderer that draws the head's path.
    /// </summary>
    public class SnakeHeadTrail : MonoBehaviour, IQualityResponsive
    {
        private int _trailLength;
        private const float TRAIL_INTERVAL = 0.04f;
        private const int ULTRA_THRESHOLD = 1000;

        // Ring buffer mode (Low/Med/High)
        private Transform[] _trailParts;
        private Renderer[] _trailRenderers;
        private int _writeIndex;

        // Line mode (Ultra)
        private List<LineRenderer> _lineSegments;
        private List<List<Vector3>> _segmentPoints;
        private bool _lineMode;
        private Color _currentLineColor;
        private Material _lineMaterial;
        private static readonly Color DefaultTrailHDR = new Color(0f, 3f, 1.2f); // bright green

        // Fade-out state (line mode)
        private Coroutine _fadeCoroutine;
        private const float FADE_DURATION = 0.4f;
        private const float FADE_SPEED_THRESHOLD = 10f;

        // Shared
        private float _nextSpawnTime;
        private SnakeGrid _grid;
        private Material _trailMaterial;
        private Color _trailBaseColor;

        public void Initialize(SnakeGrid grid, Color trailColor)
        {
            _grid = grid;
            _trailBaseColor = trailColor;
            _trailLength = QualityHints.TrailSegments(QualityBridge.CurrentTier);
            Build();
        }

        private void OnEnable()  => QualityBridge.Register(this);
        private void OnDisable() => QualityBridge.Unregister(this);

        public void OnQualityChanged(QualityTier tier)
        {
            int newLength = QualityHints.TrailSegments(tier);
            if (newLength == _trailLength) return;
            _trailLength = newLength;
            Cleanup();
            Build();
        }

        private void Build()
        {
            _lineMode = _trailLength >= ULTRA_THRESHOLD;
            if (_lineMode) BuildLineMode();
            else BuildSphereMode();
        }

        private void BuildLineMode()
        {
            _lineSegments = new List<LineRenderer>(16);
            _segmentPoints = new List<List<Vector3>>(16);
            _currentLineColor = DefaultTrailHDR;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            _lineMaterial = new Material(shader);
            _lineMaterial.SetFloat("_Surface", 0);
            _lineMaterial.SetColor("_BaseColor", Color.white);

            StartNewSegment(DefaultTrailHDR);
        }

        private void BuildSphereMode()
        {
            _trailParts = new Transform[_trailLength];
            _trailRenderers = new Renderer[_trailLength];

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");

            _trailMaterial = new Material(shader);
            Color halfAlpha = new Color(_trailBaseColor.r, _trailBaseColor.g, _trailBaseColor.b, 0.5f);
            if (_trailMaterial.HasProperty("_BaseColor"))
                _trailMaterial.SetColor("_BaseColor", halfAlpha);
            else
                _trailMaterial.color = halfAlpha;

            for (int i = 0; i < _trailLength; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Trail_{i}";
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * (SnakeBoardRenderer.CellSize * 0.3f);
                go.SetActive(false);

                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var r = go.GetComponent<Renderer>();
                r.material = new Material(_trailMaterial);

                _trailParts[i] = go.transform;
                _trailRenderers[i] = r;
            }
        }

        private void Cleanup()
        {
            if (_trailParts != null)
            {
                for (int i = 0; i < _trailParts.Length; i++)
                    if (_trailParts[i] != null)
                        Destroy(_trailParts[i].gameObject);
                _trailParts = null;
                _trailRenderers = null;
            }
            ClearLineSegments();
            _writeIndex = 0;
        }

        private void ClearLineSegments()
        {
            if (_lineSegments != null)
            {
                for (int i = 0; i < _lineSegments.Count; i++)
                    if (_lineSegments[i] != null)
                        Destroy(_lineSegments[i]);
                _lineSegments.Clear();
            }
            if (_segmentPoints != null)
                _segmentPoints.Clear();
        }

        private void Update()
        {
            if (_grid == null || _grid.IsDead || _grid.Body.Count == 0)
            {
                if (!_lineMode) HideAllSpheres();
                return;
            }

            if (Time.time >= _nextSpawnTime)
            {
                _nextSpawnTime = Time.time + TRAIL_INTERVAL;
                if (_lineMode) AppendLinePoint();
                else SpawnSpherePoint();
            }

            if (!_lineMode)
                UpdateSphereFade();
        }

        private Vector3 HeadWorldPos()
        {
            var head = _grid.Body[0];
            float cs = SnakeBoardRenderer.CellSize;
            return new Vector3(
                head.col * cs + cs * 0.5f,
                cs * 0.5f,
                head.row * cs + cs * 0.5f);
        }

        // ── Line mode ────────────────────────────────────────────

        private void AppendLinePoint()
        {
            var pos = HeadWorldPos();

            if (_lineSegments == null || _lineSegments.Count == 0) return;
            var points = _segmentPoints[_segmentPoints.Count - 1];
            var lr = _lineSegments[_lineSegments.Count - 1];

            if (points.Count > 0 &&
                Vector3.SqrMagnitude(pos - points[points.Count - 1]) < 0.001f)
                return;

            points.Add(pos);
            lr.positionCount = points.Count;
            lr.SetPosition(points.Count - 1, pos);
        }

        private void StartNewSegment(Color hdrColor)
        {
            var go = new GameObject($"TrailSeg_{_lineSegments.Count}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 0;
            lr.startWidth = SnakeBoardRenderer.CellSize * 0.25f;
            lr.endWidth = SnakeBoardRenderer.CellSize * 0.08f;
            lr.useWorldSpace = true;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.material = new Material(_lineMaterial);
            lr.material.SetColor("_BaseColor", hdrColor);
            if (lr.material.HasProperty("_EmissionColor"))
            {
                lr.material.EnableKeyword("_EMISSION");
                lr.material.SetColor("_EmissionColor", hdrColor);
            }

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            lr.colorGradient = grad;

            var ptsList = new List<Vector3>(256);

            if (_segmentPoints != null && _segmentPoints.Count > 0)
            {
                var prev = _segmentPoints[_segmentPoints.Count - 1];
                if (prev.Count > 0)
                {
                    var bridgePos = prev[prev.Count - 1];
                    ptsList.Add(bridgePos);
                    lr.positionCount = 1;
                    lr.SetPosition(0, bridgePos);
                }
            }

            _lineSegments.Add(lr);
            _segmentPoints.Add(ptsList);
        }

        public void SetColor(Color hdrColor)
        {
            if (!_lineMode) return;
            _currentLineColor = hdrColor;
            StartNewSegment(hdrColor);
        }

        public void ClearLine()
        {
            float scale = SimulationTime.Instance != null ? SimulationTime.Instance.timeScale : 1f;
            if (scale < FADE_SPEED_THRESHOLD && _lineMode && _lineSegments != null && _lineSegments.Count > 0)
            {
                if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = StartCoroutine(FadeAndClear());
            }
            else
            {
                ClearLineImmediate();
            }
        }

        private void ClearLineImmediate()
        {
            if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
            ClearLineSegments();
            _currentLineColor = DefaultTrailHDR;
            if (_lineMode) StartNewSegment(DefaultTrailHDR);
        }

        private IEnumerator FadeAndClear()
        {
            var fadingLRs = new List<LineRenderer>(_lineSegments);
            var originalBaseColors = new List<Color>(fadingLRs.Count);
            for (int i = 0; i < fadingLRs.Count; i++)
                originalBaseColors.Add(fadingLRs[i] != null && fadingLRs[i].material.HasProperty("_BaseColor")
                    ? fadingLRs[i].material.GetColor("_BaseColor")
                    : Color.white);

            _lineSegments.Clear();
            _segmentPoints.Clear();
            _currentLineColor = DefaultTrailHDR;
            StartNewSegment(DefaultTrailHDR);

            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / FADE_DURATION;
                for (int i = 0; i < fadingLRs.Count; i++)
                {
                    if (fadingLRs[i] == null) continue;
                    var mat = fadingLRs[i].material;
                    Color faded = Color.Lerp(originalBaseColors[i], Color.black, t);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", faded);
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", faded);
                }
                yield return null;
            }

            for (int i = 0; i < fadingLRs.Count; i++)
                if (fadingLRs[i] != null)
                    Destroy(fadingLRs[i].gameObject);

            _fadeCoroutine = null;
        }

        // ── Sphere mode ──────────────────────────────────────────

        private void SpawnSpherePoint()
        {
            var part = _trailParts[_writeIndex];
            part.position = HeadWorldPos();
            part.gameObject.SetActive(true);

            float s = SnakeBoardRenderer.CellSize * 0.3f;
            part.localScale = Vector3.one * s;

            ResetColor(_writeIndex);
            _writeIndex = (_writeIndex + 1) % _trailLength;
        }

        private void UpdateSphereFade()
        {
            for (int i = 0; i < _trailLength; i++)
            {
                if (!_trailParts[i].gameObject.activeSelf) continue;

                var r = _trailRenderers[i];
                Color c = r.material.HasProperty("_BaseColor")
                    ? r.material.GetColor("_BaseColor")
                    : r.material.color;

                c = Color.Lerp(c, Color.black, Time.deltaTime * 5f);
                if (c.maxColorComponent <= 0.01f)
                {
                    _trailParts[i].gameObject.SetActive(false);
                }
                else
                {
                    if (r.material.HasProperty("_BaseColor"))
                        r.material.SetColor("_BaseColor", c);
                    else
                        r.material.color = c;
                    _trailParts[i].localScale *= 0.97f;
                }
            }
        }

        private void ResetColor(int index)
        {
            var r = _trailRenderers[index];
            Color c = _trailMaterial.HasProperty("_BaseColor")
                ? _trailMaterial.GetColor("_BaseColor")
                : _trailMaterial.color;
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", c);
            else
                r.material.color = c;
        }

        private void HideAllSpheres()
        {
            if (_trailParts == null) return;
            for (int i = 0; i < _trailParts.Length; i++)
                if (_trailParts[i] != null)
                    _trailParts[i].gameObject.SetActive(false);
        }
    }
}
