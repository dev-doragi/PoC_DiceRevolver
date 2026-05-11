using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

            if (_laserLine == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _laserLine is Missing! 탄 궤적을 그릴 수 없습니다.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            TryResolveReferences();

            if (_laserLine == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _laserLine is Missing! 탄 궤적을 그릴 수 없습니다.");
                enabled = false;
                return;
            }
            
            if (EventBus.Instance == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] EventBus.Instance is null");
                return;
            }
            
            EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFired);
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }
            
            EventBus.Instance.Unsubscribe<ShotFiredEvent>(OnShotFired);
            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
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
        }

        private void TryResolveReferences()
        {
            if (_gridManager == null) _gridManager = GridManager.Instance;
        }

        #region Laser Shot Effect

        private void OnShotFired(ShotFiredEvent e)
        {
            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            _isLaserRunning = false;

            PlayLaserEffect(e);
        }

        private void OnEnemyDamaged(EnemyDamagedEvent e)
        {
            if (_gridManager == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _gridManager is null");
                return;
            }

            PlayEnemyHitPing(_gridManager.CellToWorld(e.EnemyPosition));
        }

        private void PlayLaserEffect(ShotFiredEvent e)
        {
            if (_laserLine == null)
            {
                _isLaserRunning = false;
                EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
                return;
            }

            if (_gridManager == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _gridManager is null");
                _isLaserRunning = false;
                EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
                return;
            }

            if (e.PathPoints == null || e.PathPoints.Count == 0)
            {
                _isLaserRunning = false;
                EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
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
                EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
                yield break;
            }

            _laserLine.enabled = true;
            _laserLine.startColor = shotColor;
            _laserLine.endColor = shotColor;
            _laserLine.startWidth = _laserMinWidth;
            _laserLine.endWidth = _laserMaxWidth;

            int currentSegment = 0;

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

                    _laserLine.positionCount = currentSegment + 2;
                    for (int i = 0; i <= currentSegment; i++)
                    {
                        _laserLine.SetPosition(i, worldPathPoints[i]);
                    }
                    _laserLine.SetPosition(currentSegment + 1, laserTip);

                    yield return null;
                }

                _laserLine.SetPosition(currentSegment + 1, endPoint);
                currentSegment++;
            }

            yield return new WaitForSeconds(_laserFadeDuration);

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            _isLaserRunning = false;
            EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
        }

        private void PlayEnemyHitPing(Vector3 worldPos)
        {
            if (string.IsNullOrEmpty(_hitParticleKey))
            {
                Debug.LogError("[BulletTrajectoryVisualizer] _hitParticleKey is null or empty");
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] PoolManager.Instance is null");
                return;
            }

            GameObject particleObj = PoolManager.Instance.Spawn(_hitParticleKey, worldPos, Quaternion.identity);
            if (particleObj == null)
            {
                Debug.LogError("[BulletTrajectoryVisualizer] Failed to spawn hit particle");
                return;
            }

            Renderer[] renderers = particleObj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.sortingOrder = 1000;
            }

            SortingGroup sortingGroup = particleObj.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = 1000;
            }
        }

        private Color GetShotColor(int bulletType)
        {
            BulletLogicSO bullet = CylinderSystem.Instance?.GetBulletLogic(bulletType);
            return bullet != null ? bullet.ThemeColor : Color.white;
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
        }

        #endregion
    }
}
