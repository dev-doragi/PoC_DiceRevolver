using System.Collections.Generic;
using UnityEngine;

namespace PocDiceTactics
{
    /// <summary>
    /// 탄의 궤적을 계산하고 시각적으로 표시하는 책임을 전담합니다.
    /// 단일 책임 원칙에 따라 GridViewManager 에서 탄 궤적 관련 기능을 분리했습니다.
    /// </summary>
    public class BulletTrajectoryVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        
        [Header("Trajectory Preview")]
        [SerializeField] private LineRenderer _ricochetPreviewLine;
        [SerializeField] private float _previewLineWidth = 0.045f;
        
        [Header("Laser Shot")]
        [SerializeField] private LineRenderer _laserLine;
        [SerializeField] private float _laserTravelSpeed = 24f;
        [SerializeField] private float _laserFadeDuration = 0.12f;
        [SerializeField] private float _laserMinWidth = 0.03f;
        [SerializeField] private float _laserMaxWidth = 0.1f;
        
        [Header("Particles")]
        [SerializeField] private string _hitParticleKey = "Explosion";

        private Coroutine _laserRoutine;
        private bool _isLaserRunning;

        private void Awake()
        {
            TryResolveReferences();
        }

        private void OnEnable()
        {
            TryResolveReferences();
            
            if (EventBus.Instance == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] EventBus.Instance is null");
                return;
            }
            
            EventBus.Instance.Subscribe<RicochetTrajectoryPreviewEvent>(OnRicochetTrajectoryPreview);
            EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFired);
            EventBus.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }
            
            EventBus.Instance.Unsubscribe<RicochetTrajectoryPreviewEvent>(OnRicochetTrajectoryPreview);
            EventBus.Instance.Unsubscribe<ShotFiredEvent>(OnShotFired);
            EventBus.Instance.Unsubscribe<GameOverEvent>(OnGameOver);

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            _isLaserRunning = false;
            HideRicochetPreview();
        }

        private void TryResolveReferences()
        {
            if (_gridManager == null) _gridManager = GridManager.Instance;
        }

        #region Ricochet Trajectory Preview

        private void OnRicochetTrajectoryPreview(RicochetTrajectoryPreviewEvent e)
        {
            RenderPreciseLaserPreview(e);
        }

        private void RenderPreciseLaserPreview(RicochetTrajectoryPreviewEvent e)
        {
            if (_ricochetPreviewLine == null)
            {
                return;
            }

            if (!e.IsActive)
            {
                HideRicochetPreview();
                return;
            }

            if (_gridManager == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] GridManager is null");
                HideRicochetPreview();
                return;
            }

            if (e.Direction == Vector2Int.zero)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] direction is zero in RenderPreciseLaserPreview");
                HideRicochetPreview();
                return;
            }

            GridManager.LaserLogicResult logicResult = e.LogicResult;
            if (logicResult.PassedTiles == null)
            {
                logicResult = _gridManager.CalculateLaserLogic(e.Origin, e.Direction);
            }

            List<Vector3> points = BuildPreciseLaserPoints(_gridManager, e.Origin, e.Direction, logicResult);
            if (points == null || points.Count < 2)
            {
                HideRicochetPreview();
                return;
            }

            _ricochetPreviewLine.enabled = true;
            _ricochetPreviewLine.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                _ricochetPreviewLine.SetPosition(i, points[i]);
            }

            _ricochetPreviewLine.startWidth = _previewLineWidth;
            _ricochetPreviewLine.endWidth = _previewLineWidth;
            Color previewColor = new Color(1f, 0.25f, 0.25f, 0.65f);
            _ricochetPreviewLine.startColor = previewColor;
            _ricochetPreviewLine.endColor = previewColor;
        }

        private List<Vector3> BuildPreciseLaserPoints(GridManager gridManager, Vector2Int origin, Vector2Int direction, GridManager.LaserLogicResult logicResult)
        {
            if (gridManager == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] gridManager is null in BuildPreciseLaserPoints");
                return null;
            }

            Vector3 startWorld = gridManager.CellToWorld(origin);
            Vector3 endWorld = startWorld;

            if (logicResult.HitWall)
            {
                if (!TryGetPreciseWallHitPoint(gridManager, startWorld, direction, logicResult.HitWallTile, out endWorld))
                {
                    endWorld = gridManager.CellToWorld(logicResult.HitWallTile);
                }
            }
            else if (logicResult.PassedTiles != null && logicResult.PassedTiles.Count > 0)
            {
                endWorld = gridManager.CellToWorld(logicResult.PassedTiles[logicResult.PassedTiles.Count - 1]);
            }
            else
            {
                Vector2Int farTile = origin + direction * Mathf.Max(gridManager.GridSize.x, gridManager.GridSize.y);
                if (gridManager.IsInside(farTile))
                {
                    endWorld = gridManager.CellToWorld(farTile);
                }
                else
                {
                    endWorld = startWorld + new Vector3(direction.x, direction.y, 0f) * gridManager.CellSize * Mathf.Max(gridManager.GridSize.x, gridManager.GridSize.y);
                }
            }

            return new List<Vector3> { startWorld, endWorld };
        }

        private bool TryGetPreciseWallHitPoint(GridManager gridManager, Vector3 startWorld, Vector2Int direction, Vector2Int wallTile, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;

            if (gridManager == null)
            {
                return false;
            }

            Vector3 wallCenter = gridManager.CellToWorld(wallTile);
            float halfCell = gridManager.CellSize * 0.5f;

            Vector3 dirVec = new Vector3(direction.x, direction.y, 0f).normalized;

            if (direction.x != 0)
            {
                float hitX = direction.x > 0 ? wallCenter.x - halfCell : wallCenter.x + halfCell;
                float t = (hitX - startWorld.x) / dirVec.x;
                float hitY = startWorld.y + dirVec.y * t;

                if (Mathf.Abs(hitY - wallCenter.y) <= halfCell * 1.05f)
                {
                    hitPoint = new Vector3(hitX, hitY, 0f);
                    return true;
                }
            }

            if (direction.y != 0)
            {
                float hitY = direction.y > 0 ? wallCenter.y - halfCell : wallCenter.y + halfCell;
                float t = (hitY - startWorld.y) / dirVec.y;
                float hitX = startWorld.x + dirVec.x * t;

                if (Mathf.Abs(hitX - wallCenter.x) <= halfCell * 1.05f)
                {
                    hitPoint = new Vector3(hitX, hitY, 0f);
                    return true;
                }
            }

            return false;
        }

        private void HideRicochetPreview()
        {
            if (_ricochetPreviewLine != null)
            {
                _ricochetPreviewLine.enabled = false;
            }
        }

        #endregion

        #region Laser Shot Effect

        private void OnShotFired(ShotFiredEvent e)
        {
            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            HideRicochetPreview();
            _isLaserRunning = false;

            PlayLaserEffect(e);
        }

        private void PlayLaserEffect(ShotFiredEvent e)
        {
            if (_laserLine == null)
            {
                _isLaserRunning = false;
                return;
            }

            if (_gridManager == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _gridManager is null");
                _isLaserRunning = false;
                return;
            }

            if (e.PathPoints == null || e.PathPoints.Count == 0)
            {
                _isLaserRunning = false;
                return;
            }

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
            }

            _isLaserRunning = true;
            List<Vector3> rawPath = new List<Vector3>(e.PathPoints.Count + 1)
            {
                _gridManager.CellToWorld(e.Origin)
            };
            rawPath.AddRange(e.PathPoints);

            List<Vector3> straightPath = SmoothLaserPath(rawPath, e.BulletType);
            _laserRoutine = StartCoroutine(PlayLaserTravelRoutine(straightPath, GetShotColor(e.BulletType)));
        }

        private List<Vector3> SmoothLaserPath(List<Vector3> rawPath, int bulletType)
        {
            if (rawPath == null || rawPath.Count <= 2)
            {
                return rawPath;
            }

            if (_gridManager == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _gridManager is null");
                return rawPath;
            }

            List<Vector3> smoothPath = new List<Vector3>();
            smoothPath.Add(rawPath[0]);

            if (bulletType != 3)
            {
                smoothPath.Add(rawPath[rawPath.Count - 1]);
                return smoothPath;
            }

            int lastSignX = 0;
            int lastSignY = 0;
            Vector2Int prevCell = _gridManager.WorldToCell(rawPath[0]);

            for (int i = 1; i < rawPath.Count - 1; i++)
            {
                Vector2Int currCell = _gridManager.WorldToCell(rawPath[i]);
                if (currCell == prevCell)
                {
                    continue;
                }

                Vector2Int step = currCell - prevCell;
                int signX = step.x == 0 ? 0 : (int)Mathf.Sign(step.x);
                int signY = step.y == 0 ? 0 : (int)Mathf.Sign(step.y);

                bool isBounce = false;
                if (signX != 0 && lastSignX != 0 && signX != lastSignX)
                {
                    isBounce = true;
                }

                if (signY != 0 && lastSignY != 0 && signY != lastSignY)
                {
                    isBounce = true;
                }

                if (isBounce)
                {
                    smoothPath.Add(_gridManager.CellToWorld(prevCell));
                    lastSignX = signX;
                    lastSignY = signY;
                }
                else
                {
                    if (signX != 0)
                    {
                        lastSignX = signX;
                    }

                    if (signY != 0)
                    {
                        lastSignY = signY;
                    }
                }

                prevCell = currCell;
            }

            smoothPath.Add(rawPath[rawPath.Count - 1]);
            return smoothPath;
        }

        private IEnumerator PlayLaserTravelRoutine(List<Vector3> worldPathPoints, Color shotColor)
        {
            if (worldPathPoints == null || worldPathPoints.Count < 2)
            {
                _isLaserRunning = false;
                yield break;
            }

            _laserLine.enabled = true;
            _laserLine.positionCount = 2;
            _laserLine.SetPosition(0, worldPathPoints[0]);
            _laserLine.SetPosition(1, worldPathPoints[0]);
            _laserLine.startColor = shotColor;
            _laserLine.endColor = shotColor;
            _laserLine.startWidth = _laserMinWidth;
            _laserLine.endWidth = _laserMaxWidth;

            int currentSegment = 0;
            float distanceTraveled = 0f;

            while (currentSegment < worldPathPoints.Count - 1)
            {
                Vector3 startPoint = worldPathPoints[currentSegment];
                Vector3 endPoint = worldPathPoints[currentSegment + 1];
                float segmentLength = Vector3.Distance(startPoint, endPoint);

                if (segmentLength < 0.001f)
                {
                    currentSegment++;
                    continue;
                }

                float travelTime = segmentLength / _laserTravelSpeed;
                float elapsed = 0f;

                while (elapsed < travelTime)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / travelTime);
                    Vector3 laserTip = Vector3.Lerp(startPoint, endPoint, t);

                    _laserLine.positionCount = 2;
                    _laserLine.SetPosition(0, worldPathPoints[0]);
                    _laserLine.SetPosition(1, laserTip);

                    yield return null;
                }

                _laserLine.SetPosition(1, endPoint);
                currentSegment++;
            }

            yield return new WaitForSeconds(_laserFadeDuration);

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            _isLaserRunning = false;
        }

        private Color GetShotColor(int bulletType)
        {
            switch (bulletType)
            {
                case 0: return new Color(1f, 1f, 1f, 1f);
                case 1: return new Color(1f, 0.5f, 0f, 1f);
                case 2: return new Color(0f, 1f, 1f, 1f);
                case 3: return new Color(1f, 0.25f, 0.25f, 1f);
                case 4: return new Color(0.5f, 0f, 1f, 1f);
                case 5: return new Color(1f, 1f, 0f, 1f);
                default: return new Color(1f, 1f, 1f, 1f);
            }
        }

        #endregion

        #region Game Over Cleanup

        private void OnGameOver(GameOverEvent _)
        {
            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            _isLaserRunning = false;
            HideRicochetPreview();
        }

        #endregion
    }
}
