using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.Rendering;

namespace PocDiceTactics
{
    /// <summary>
    /// 그리드 타일 뷰를 생성/관리하고 이벤트를 받아 타일 연출을 갱신합니다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class GridViewManager : MonoBehaviour
    {
        // Note: Ensure the particle prefab for _hitParticleKey is pre-created via PoolManager.CreatePool and has DespawnController component attached.

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
        [SerializeField] private GameObject _damagePopupPrefab;
        [SerializeField] private Transform _damagePopupCanvasRoot;
        [SerializeField] private string _hitParticleKey = "Explosion";

        private readonly Dictionary<Vector2Int, TileView> _tileMap = new Dictionary<Vector2Int, TileView>();
        private readonly HashSet<Vector2Int> _telegraphCells = new HashSet<Vector2Int>();
        private Coroutine _shotRoutine;
        private Coroutine _laserRoutine;

        private bool _hasHoverCell;
        private Vector2Int _hoverCell;

        private bool _hasGhostCell;
        private Vector2Int _ghostCell;
        private bool _isShotVisualActive;
        private bool _isShotPathRunning;
        private bool _isLaserRunning;

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
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
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
            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
                _laserRoutine = null;
            }

            if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            _isShotVisualActive = false;
            _isShotPathRunning = false;
            _isLaserRunning = false;
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
                    tile.SetOverheated(_gridManager.IsOverheated(cell));

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

            _isShotVisualActive = true;
            _isShotPathRunning = true;
            _isLaserRunning = false;

            PlayLaserEffect(e);
            _shotRoutine = StartCoroutine(PlayShotPathRoutine(e));
        }

        private void OnEnemyDamaged(EnemyDamagedEvent e)
        {
            if (_gridManager == null)
            {
                Debug.LogError("[GridViewManager] _gridManager is null");
                return;
            }

            Vector3 worldPos = _gridManager.CellToWorld(e.EnemyPosition);
            PlayEnemyHitPing(worldPos);
            SpawnDamagePopup(worldPos, e.Damage);
        }

        private void PlayLaserEffect(ShotFiredEvent e)
        {
            if (_laserLine == null)
            {
                _isLaserRunning = false;
                TryCompleteShotVisualSequence();
                return;
            }

            if (e.PathPoints == null || e.PathPoints.Count == 0)
            {
                _isLaserRunning = false;
                TryCompleteShotVisualSequence();
                return;
            }

            if (_laserRoutine != null)
            {
                StopCoroutine(_laserRoutine);
            }

            _isLaserRunning = true;
            List<Vector3> straightPath = SmoothLaserPath(e.PathPoints, e.BulletType);
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
                Debug.LogError("[GridViewManager] _gridManager is null");
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

        private IEnumerator PlayShotPathRoutine(ShotFiredEvent e)
        {
            Color shotColor = GetShotColor(e.BulletType);
            if (_gridManager == null)
            {
                _shotRoutine = null;
                _isShotPathRunning = false;
                TryCompleteShotVisualSequence();
                yield break;
            }

            List<Vector3> shotPath = e.PathPoints ?? new List<Vector3>();

            for (int i = 0; i < shotPath.Count; i++)
            {
                Vector2Int cell = _gridManager.WorldToCell(shotPath[i]);
                if (!_gridManager.IsInside(cell))
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
            _isShotPathRunning = false;
            TryCompleteShotVisualSequence();
        }

        private IEnumerator PlayLaserTravelRoutine(List<Vector3> worldPathPoints, Color shotColor)
        {
            if (_laserLine == null || worldPathPoints == null || worldPathPoints.Count < 2)
            {
                _laserRoutine = null;
                _isLaserRunning = false;
                TryCompleteShotVisualSequence();
                yield break;
            }

            // 1. 전체 궤적의 길이 계산
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
                _isLaserRunning = false;
                TryCompleteShotVisualSequence();
                yield break;
            }

            float travelDuration = Mathf.Max(0.03f, totalDistance / Mathf.Max(0.001f, _laserTravelSpeed));
            float elapsed = 0f;
            _laserLine.enabled = true;

            // 2. 레이저 이동 애니메이션
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                float width = Mathf.Lerp(_laserMinWidth, _laserMaxWidth, pulse);
                float distanceTraveled = totalDistance * t;

                // 현재 어느 선분(벽에 튕긴 후의 구간)을 지나고 있는지 계산
                float remaining = distanceTraveled;
                int currentSegmentIndex = 0;
                for (int i = 0; i < segmentLengths.Count; i++)
                {
                    if (remaining <= segmentLengths[i] || i == segmentLengths.Count - 1)
                    {
                        currentSegmentIndex = i;
                        break;
                    }
                    remaining -= segmentLengths[i];
                }

                // 현재 레이저의 끝(Head) 위치 계산
                float segmentT = segmentLengths[currentSegmentIndex] > 0 ? Mathf.Clamp01(remaining / segmentLengths[currentSegmentIndex]) : 1f;
                Vector3 head = Vector3.Lerp(worldPathPoints[currentSegmentIndex], worldPathPoints[currentSegmentIndex + 1], segmentT);

                _laserLine.positionCount = currentSegmentIndex + 2;
                for (int i = 0; i <= currentSegmentIndex; i++)
                {
                    // 지나온 모서리들은 고정
                    _laserLine.SetPosition(i, worldPathPoints[i]);
                }
                // 가장 앞부분은 날아가는 중인 머리 위치
                _laserLine.SetPosition(currentSegmentIndex + 1, head);

                _laserLine.startWidth = width;
                _laserLine.endWidth = width;
                Color color = shotColor;
                color.a = 1f;
                _laserLine.startColor = color;
                _laserLine.endColor = color;

                yield return null;
            }

            // 3. 이동이 끝난 후에는 전체 궤적(모든 관절)을 완벽히 세팅
            _laserLine.positionCount = worldPathPoints.Count;
            for (int i = 0; i < worldPathPoints.Count; i++)
            {
                _laserLine.SetPosition(i, worldPathPoints[i]);
            }

            // 4. 서서히 사라지는 페이드 아웃 연출
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
            _isLaserRunning = false;
            TryCompleteShotVisualSequence();
        }

        private void TryCompleteShotVisualSequence()
        {
            if (!_isShotVisualActive)
            {
                return;
            }

            if (_isShotPathRunning || _isLaserRunning)
            {
                return;
            }

            _isShotVisualActive = false;
        }

        private void PlayEnemyHitPing(Vector3 worldPos)
        {
            if (string.IsNullOrEmpty(_hitParticleKey))
            {
                Debug.LogError("[GridViewManager] _hitParticleKey is null or empty");
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[GridViewManager] PoolManager.Instance is null");
                return;
            }

            GameObject particleObj = PoolManager.Instance.Spawn(_hitParticleKey, worldPos, Quaternion.identity);
            if (particleObj == null)
            {
                Debug.LogError("[GridViewManager] Failed to spawn hit particle");
                return;
            }

            // Temporarily set sorting order to very high value to ensure it's on top
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

        private void SpawnDamagePopup(Vector3 worldPos, int damage)
        {
            if (_damagePopupPrefab == null)
            {
                Debug.LogError("[GridViewManager] _damagePopupPrefab is null");
                return;
            }

            if (_damagePopupCanvasRoot == null)
            {
                Debug.LogError("[GridViewManager] _damagePopupCanvasRoot is null");
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[GridViewManager] PoolManager.Instance is null");
                return;
            }

            GameObject popupObject = PoolManager.Instance.Spawn(_damagePopupPrefab.name, worldPos, Quaternion.identity);
            if (popupObject == null)
            {
                Debug.LogError("[GridViewManager] Failed to spawn damage popup");
                return;
            }

            popupObject.transform.SetParent(_damagePopupCanvasRoot, false);
            popupObject.transform.position = worldPos;

            DamagePopup popup = popupObject.GetComponent<DamagePopup>();
            if (popup == null)
            {
                Debug.LogError("[GridViewManager] DamagePopup component is missing");
                PoolManager.Instance.Despawn(popupObject);
                return;
            }

            popup.Play(damage);
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

        private Color GetShotColor(int bulletType)
        {
            switch (bulletType)
            {
                case 0:
                case 1:
                case 2:
                    return new Color(1f, 0.88f, 0.32f, 0.65f);
                case 3:
                case 4:
                    return new Color(1f, 0.25f, 0.25f, 0.72f);
                case 5:
                    return new Color(0.9f, 0.9f, 1f, 0.62f);
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