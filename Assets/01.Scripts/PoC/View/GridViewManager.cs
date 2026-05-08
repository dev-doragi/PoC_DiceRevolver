using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace PocDiceTactics
{
    /// <summary>
    /// 그리드 타일 뷰를 생성/관리하고 이벤트를 받아 타일 연출을 갱신합니다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class GridViewManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private Transform _tileRoot;

        [Header("Placement")]
        [SerializeField] private Vector3 _tileOffset = Vector3.zero;
        [SerializeField] private float _shotStepInterval = 0.035f;
        [SerializeField] private LineRenderer _laserLine;
        [SerializeField] private float _laserTravelSpeed = 24f;
        [SerializeField] private float _laserFadeDuration = 0.12f;
        [SerializeField] private float _laserMinWidth = 0.03f;
        [SerializeField] private float _laserMaxWidth = 0.1f;

        private readonly Dictionary<Vector2Int, TileView> _tileMap = new Dictionary<Vector2Int, TileView>();
        private readonly HashSet<Vector2Int> _telegraphCells = new HashSet<Vector2Int>();
        private Coroutine _shotRoutine;
        private Coroutine _laserRoutine;

        private bool _hasHoverCell;
        private Vector2Int _hoverCell;

        private bool _hasGhostCell;
        private Vector2Int _ghostCell;

        private void Start()
        {
            TryResolveReferences();
            BuildGridViews();
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<RoundStartedEvent>(OnRoundStarted);
            EventBus.Instance.Subscribe<TileOverheatedEvent>(OnTileOverheated);
            EventBus.Instance.Subscribe<TileCooledEvent>(OnTileCooled);
            EventBus.Instance.Subscribe<TileHoverEvent>(OnTileHover);
            EventBus.Instance.Subscribe<MoveGhostEvent>(OnMoveGhost);
            EventBus.Instance.Subscribe<PlayerMovedEvent>(OnPlayerMoved);
            EventBus.Instance.Subscribe<EnemyTelegraphEvent>(OnEnemyTelegraph);
            EventBus.Instance.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFired);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
            EventBus.Instance.Unsubscribe<TileOverheatedEvent>(OnTileOverheated);
            EventBus.Instance.Unsubscribe<TileCooledEvent>(OnTileCooled);
            EventBus.Instance.Unsubscribe<TileHoverEvent>(OnTileHover);
            EventBus.Instance.Unsubscribe<MoveGhostEvent>(OnMoveGhost);
            EventBus.Instance.Unsubscribe<PlayerMovedEvent>(OnPlayerMoved);
            EventBus.Instance.Unsubscribe<EnemyTelegraphEvent>(OnEnemyTelegraph);
            EventBus.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Instance.Unsubscribe<ShotFiredEvent>(OnShotFired);

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }
        }

        private void TryResolveReferences()
        {
            if (_gridManager == null) _gridManager = GridManager.Instance;
            if (_tileRoot == null) _tileRoot = transform;
        }

        private void BuildGridViews()
        {
            if (_gridManager == null || _tilePrefab == null || _tileRoot == null) return;

            ClearGridViews();

            Vector2Int size = _gridManager.GridSize;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);

                    TileView tile = Instantiate(_tilePrefab, _tileRoot);
                    tile.name = $"Tile_{x}_{y}";
                    tile.transform.position = _gridManager.CellToWorld(cell) + _tileOffset;
                    tile.Initialize(cell, _gridManager.IsWall(cell));

                    _tileMap[cell] = tile;
                }
            }

            _telegraphCells.Clear();
            _hasHoverCell = false;
            _hasGhostCell = false;
        }

        private void ClearGridViews()
        {
            foreach (Transform child in _tileRoot)
            {
                Destroy(child.gameObject);
            }
            _tileMap.Clear();
        }

        private void OnRoundStarted(RoundStartedEvent _)
        {
            TryResolveReferences();
            BuildGridViews();
        }

        private void OnTileOverheated(TileOverheatedEvent e)
        {
            if (_tileMap.TryGetValue(e.Cell, out TileView tile))
            {
                tile.SetOverheated(true);
            }
        }

        private void OnTileCooled(TileCooledEvent e)
        {
            if (_tileMap.TryGetValue(e.Cell, out TileView tile))
            {
                tile.SetOverheated(false);
            }
        }

        private void OnTileHover(TileHoverEvent e)
        {
            ClearHoverOnly();
            if (_tileMap.TryGetValue(e.Cell, out TileView tile))
            {
                tile.SetHover(true, e.PredictedTopFace, false);
                _hoverCell = e.Cell;
                _hasHoverCell = true;
            }
        }

        private void OnMoveGhost(MoveGhostEvent e)
        {
            ClearGhostOnly();
            if (_tileMap.TryGetValue(e.TargetCell, out TileView tile))
            {
                tile.SetHover(true, e.PredictedTopFace, e.IsConfirmRequired);
                _ghostCell = e.TargetCell;
                _hasGhostCell = true;
            }
        }

        private void OnPlayerMoved(PlayerMovedEvent _)
        {
            ClearHoverOnly();
            ClearGhostOnly();
        }

        private void OnEnemyTelegraph(EnemyTelegraphEvent e)
        {
            if (!_tileMap.TryGetValue(e.TargetCell, out TileView tile)) return;

            if (e.IsActive)
            {
                _telegraphCells.Add(e.TargetCell);
                tile.SetTelegraph(true);
            }
            else
            {
                _telegraphCells.Remove(e.TargetCell);
                tile.SetTelegraph(false);
            }
        }

        private void OnGameOver(GameOverEvent _)
        {
            if (_shotRoutine != null)
            {
                StopCoroutine(_shotRoutine);
                _shotRoutine = null;
            }

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            foreach (var pair in _tileMap)
            {
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(true);
                    pair.Value.ClearTransient();
                }
            }
            _telegraphCells.Clear();
            _hasHoverCell = false;
            _hasGhostCell = false;
        }

        private void OnShotFired(ShotFiredEvent e)
        {
            if (_shotRoutine != null)
            {
                StopCoroutine(_shotRoutine);
            }

            PlayLaserEffect(e);
            _shotRoutine = StartCoroutine(PlayShotPathRoutine(e));
        }

        private void PlayLaserEffect(ShotFiredEvent e)
        {
            if (_gridManager == null || _laserLine == null)
            {
                return;
            }

            List<Vector2Int> shotPath = CalculateShotPath(e);
            if (shotPath.Count == 0)
            {
                return;
            }

            List<Vector3> worldPath = new List<Vector3>(shotPath.Count + 1)
            {
                _gridManager.CellToWorld(e.Origin)
            };

            Vector2Int previousStep = shotPath[0] - e.Origin;

            for (int i = 1; i < shotPath.Count; i++)
            {
                Vector2Int currentCell = shotPath[i];
                Vector2Int currentStep = currentCell - shotPath[i - 1];

                int directionDot = (previousStep.x * currentStep.x) + (previousStep.y * currentStep.y);
                if (directionDot <= 0)
                {
                    Vector3 turnPoint = _gridManager.CellToWorld(shotPath[i - 1]);
                    if (worldPath[worldPath.Count - 1] != turnPoint)
                    {
                        worldPath.Add(turnPoint);
                    }
                }

                previousStep = currentStep;
            }

            Vector3 finalPoint = _gridManager.CellToWorld(shotPath[shotPath.Count - 1]);
            if (worldPath[worldPath.Count - 1] != finalPoint)
            {
                worldPath.Add(finalPoint);
            }

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
            }

            _laserRoutine = StartCoroutine(PlayLaserTravelRoutine(worldPath, GetShotColor(e.BulletType)));
        }

        private List<Vector2Int> CalculateShotPath(ShotFiredEvent e)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            if (_gridManager == null)
            {
                return path;
            }

            if (e.Direction == Vector2Int.zero)
            {
                return path;
            }

            if (e.BulletType == 6)
            {
                foreach (Vector2Int cell in _gridManager.GetBresenhamLine(e.Origin, e.Direction))
                {
                    if (!_gridManager.IsInside(cell))
                    {
                        break;
                    }

                    path.Add(cell);
                }

                return path;
            }

            if (IsRicochetBullet(e.BulletType))
            {
                Vector2Int currentOrigin = e.Origin;
                Vector2Int rayDelta = e.Direction;
                int bounceCount = 0;
                while (true)
                {
                    bool advanced = false;
                    foreach (Vector2Int cell in _gridManager.GetBresenhamLine(currentOrigin, rayDelta))
                    {
                        if (!_gridManager.IsInside(cell))
                        {
                            return path;
                        }

                        path.Add(cell);

                        if (_gridManager.IsWall(cell))
                        {
                            bounceCount++;
                            if (bounceCount > 1)
                            {
                                return path;
                            }

                            Vector2Int step = cell - currentOrigin;
                            Vector2Int reflectedStep = ReflectRicochetStep(currentOrigin, step);
                            if (reflectedStep == Vector2Int.zero)
                            {
                                return path;
                            }

                            int remainingX = rayDelta.x - step.x;
                            int remainingY = rayDelta.y - step.y;
                            rayDelta = new Vector2Int(reflectedStep.x + remainingX, reflectedStep.y + remainingY);
                            currentOrigin = cell;
                            advanced = true;
                            break;
                        }

                        if (_gridManager.GetOccupant(cell) is EnemyController)
                        {
                            return path;
                        }

                        currentOrigin = cell;
                        advanced = true;
                    }

                    if (!advanced || rayDelta == Vector2Int.zero)
                    {
                        return path;
                    }
                }
            }

            if (e.BulletType == 3)
            {
                foreach (Vector2Int cell in _gridManager.GetBresenhamLine(e.Origin, e.Direction))
                {
                    if (!_gridManager.IsInside(cell))
                    {
                        break;
                    }

                    path.Add(cell);

                    if (_gridManager.IsWall(cell))
                    {
                        break;
                    }
                }

                return path;
            }

            foreach (Vector2Int cell in _gridManager.GetBresenhamLine(e.Origin, e.Direction))
            {
                if (!_gridManager.IsInside(cell))
                {
                    break;
                }

                path.Add(cell);

                if (_gridManager.IsWall(cell))
                {
                    break;
                }

                if (_gridManager.GetOccupant(cell) is EnemyController)
                {
                    break;
                }
            }

            return path;
        }

        private IEnumerator PlayShotPathRoutine(ShotFiredEvent e)
        {
            Color shotColor = GetShotColor(e.BulletType);
            List<Vector2Int> shotPath = CalculateShotPath(e);

            for (int i = 0; i < shotPath.Count; i++)
            {
                Vector2Int cell = shotPath[i];
                if (_gridManager == null || !_gridManager.IsInside(cell))
                {
                    break;
                }

                if (_tileMap.TryGetValue(cell, out TileView tile))
                {
                    tile.PlayShotFlash(shotColor);
                }

                if (i < shotPath.Count - 1)
                {
                    yield return new WaitForSeconds(_shotStepInterval);
                }
            }

            _shotRoutine = null;
        }

        private Vector2Int ReflectRicochetStep(Vector2Int origin, Vector2Int step)
        {
            int sx = step.x == 0 ? 0 : (step.x > 0 ? 1 : -1);
            int sy = step.y == 0 ? 0 : (step.y > 0 ? 1 : -1);

            if (sx == 0 && sy == 0)
            {
                return Vector2Int.zero;
            }

            bool hitX = false;
            bool hitY = false;

            if (sx != 0)
            {
                Vector2Int cellX = origin + new Vector2Int(sx, 0);
                hitX = !_gridManager.IsInside(cellX) || _gridManager.IsWall(cellX);
            }

            if (sy != 0)
            {
                Vector2Int cellY = origin + new Vector2Int(0, sy);
                hitY = !_gridManager.IsInside(cellY) || _gridManager.IsWall(cellY);
            }

            if (hitX) sx = -sx;
            if (hitY) sy = -sy;

            if (!hitX && !hitY)
            {
                sx = -sx;
                sy = -sy;
            }

            return new Vector2Int(sx, sy);
        }

        private IEnumerator PlayLaserTravelRoutine(List<Vector3> worldPathPoints, Color shotColor)
        {
            if (_laserLine == null || worldPathPoints == null || worldPathPoints.Count < 2)
            {
                _laserRoutine = null;
                yield break;
            }

            float totalDistance = 0f;
            List<float> segmentLengths = new List<float>(worldPathPoints.Count - 1);
            for (int i = 0; i < worldPathPoints.Count - 1; i++)
            {
                float segment = Vector3.Distance(worldPathPoints[i], worldPathPoints[i + 1]);
                segmentLengths.Add(segment);
                totalDistance += segment;
            }

            if (totalDistance <= 0.0001f)
            {
                _laserRoutine = null;
                yield break;
            }

            Vector3 startWorld = worldPathPoints[0];
            Vector3 endWorld = worldPathPoints[worldPathPoints.Count - 1];
            _laserLine.positionCount = 2;
            _laserLine.SetPosition(0, startWorld);
            _laserLine.SetPosition(1, startWorld);
            float elapsed = 0f;
            float travelDuration = Mathf.Max(0.03f, totalDistance / Mathf.Max(0.001f, _laserTravelSpeed));

            _laserLine.enabled = true;

            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float width = Mathf.Lerp(_laserMinWidth, _laserMaxWidth, pulse);
                float distanceTraveled = totalDistance * t;

                Vector3 head = EvaluatePointOnPath(worldPathPoints, segmentLengths, distanceTraveled);
                _laserLine.SetPosition(1, head);

                _laserLine.startWidth = width;
                _laserLine.endWidth = width;
                Color color = shotColor;
                color.a = 1f;

                _laserLine.startColor = color;
                _laserLine.endColor = color;

                yield return null;
            }

            _laserLine.SetPosition(1, endWorld);

            elapsed = 0f;
            float fadeDuration = Mathf.Max(0.01f, _laserFadeDuration);
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float width = Mathf.Lerp(_laserMinWidth, 0f, t);

                _laserLine.startWidth = width;
                _laserLine.endWidth = width;

                Color color = shotColor;
                color.a = Mathf.Lerp(1f, 0f, t);

                _laserLine.startColor = color;
                _laserLine.endColor = color;

                yield return null;
            }

            _laserLine.enabled = false;
            _laserLine.startWidth = _laserMinWidth;
            _laserLine.endWidth = _laserMinWidth;
            _laserRoutine = null;
        }

        private Vector3 EvaluatePointOnPath(List<Vector3> worldPathPoints, List<float> segmentLengths, float traveledDistance)
        {
            float remaining = Mathf.Max(0f, traveledDistance);

            for (int i = 0; i < segmentLengths.Count; i++)
            {
                float segmentLength = segmentLengths[i];
                if (remaining <= segmentLength || i == segmentLengths.Count - 1)
                {
                    if (segmentLength <= 0.0001f)
                    {
                        return worldPathPoints[i + 1];
                    }

                    float t = Mathf.Clamp01(remaining / segmentLength);
                    return Vector3.Lerp(worldPathPoints[i], worldPathPoints[i + 1], t);
                }

                remaining -= segmentLength;
            }

            return worldPathPoints[worldPathPoints.Count - 1];
        }

        private bool IsRicochetBullet(int bulletType)
        {
            return bulletType == 4;
        }

        private Color GetShotColor(int bulletType)
        {
            switch (bulletType)
            {
                case 1:
                case 2:
                    return new Color(1f, 0.88f, 0.32f, 0.65f);
                case 3:
                case 4:
                    return new Color(1f, 0.25f, 0.25f, 0.72f);
                case 5:
                    return new Color(0.9f, 0.9f, 1f, 0.62f);
                case 6:
                    return new Color(0.3f, 0.95f, 1f, 0.68f);
                default:
                    return new Color(1f, 1f, 1f, 0.55f);
            }
        }

        private void ClearHoverOnly()
        {
            if (!_hasHoverCell) return;
            if (_tileMap.TryGetValue(_hoverCell, out TileView tile))
            {
                tile.SetHover(false, 0, false);
            }
            _hasHoverCell = false;
        }

        private void ClearGhostOnly()
        {
            if (!_hasGhostCell) return;
            if (_tileMap.TryGetValue(_ghostCell, out TileView tile))
            {
                tile.SetHover(false, 0, false);
            }
            _hasGhostCell = false;
        }

    }
}