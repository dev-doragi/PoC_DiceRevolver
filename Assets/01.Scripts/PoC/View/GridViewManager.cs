using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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
        [SerializeField] private GameObject _damagePopupPrefab;
        [SerializeField] private Transform _damagePopupCanvasRoot;

        private readonly Dictionary<Vector2Int, TileView> _tileMap = new Dictionary<Vector2Int, TileView>();
        private readonly HashSet<Vector2Int> _telegraphCells = new HashSet<Vector2Int>();

        private bool _hasHoverCell;
        private Vector2Int _hoverCell;
        private readonly HashSet<Vector2Int> _turnStartHighlightCells = new HashSet<Vector2Int>();

        private bool _hasGhostCell;
        private Vector2Int _ghostCell;

        private void Start()
        {
            TryResolveReferences();
            BuildGridViews();
        }

        private void OnEnable()
        {
            TryResolveReferences();
            EventBus.Instance.Subscribe<RoundStartedEvent>(OnRoundStarted);
            EventBus.Instance.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Instance.Subscribe<TileOverheatedEvent>(OnTileOverheated);
            EventBus.Instance.Subscribe<TileCooledEvent>(OnTileCooled);
            EventBus.Instance.Subscribe<TileHoverEvent>(OnTileHover);
            EventBus.Instance.Subscribe<MoveGhostEvent>(OnMoveGhost);
            EventBus.Instance.Subscribe<PlayerMovedEvent>(OnPlayerMoved);
            EventBus.Instance.Subscribe<EnemyTelegraphEvent>(OnEnemyTelegraph);
            EventBus.Instance.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Subscribe<WallDestroyedEvent>(OnWallDestroyed);
        }

        private void OnDisable()
        {
            EventBus.Instance.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
            EventBus.Instance.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Instance.Unsubscribe<TileOverheatedEvent>(OnTileOverheated);
            EventBus.Instance.Unsubscribe<TileCooledEvent>(OnTileCooled);
            EventBus.Instance.Unsubscribe<TileHoverEvent>(OnTileHover);
            EventBus.Instance.Unsubscribe<MoveGhostEvent>(OnMoveGhost);
            EventBus.Instance.Unsubscribe<PlayerMovedEvent>(OnPlayerMoved);
            EventBus.Instance.Unsubscribe<EnemyTelegraphEvent>(OnEnemyTelegraph);
            EventBus.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Unsubscribe<WallDestroyedEvent>(OnWallDestroyed);
            ClearTurnStartHighlights();
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
            _turnStartHighlightCells.Clear();
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

            if (e.PredictedTopFace <= 0)
            {
                return;
            }

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
            if (_gridManager != null && _gridManager.IsWall(e.TargetCell))
            {
                return; // 벽 타일은 이동 하이라이팅 방지
            }
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
            ShowTurnStartHighlights();
        }

        private void OnPhaseChanged(PhaseChangedEvent e)
        {
            if (e.Phase == TurnPhase.PlayerTurn)
            {
                ShowTurnStartHighlights();
                return;
            }

            ClearTurnStartHighlights();
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
            ClearTurnStartHighlights();
        }

        private void ShowTurnStartHighlights()
        {
            ClearTurnStartHighlights();

            if (_gridManager == null)
            {
                Debug.LogError("[GridViewManager] _gridManager is null");
                return;
            }

            if (PlayerManager.Instance == null || PlayerManager.Instance.PlayerTransform == null)
            {
                Debug.LogError("[GridViewManager] PlayerManager.Instance or PlayerTransform is null");
                return;
            }

            Vector2Int playerCell = _gridManager.WorldToCell(PlayerManager.Instance.PlayerTransform.position);
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int targetCell = playerCell + directions[i];
                if (!_gridManager.IsWalkable(targetCell))
                {
                    continue;
                }

                if (_tileMap.TryGetValue(targetCell, out TileView tile))
                {
                    tile.SetHover(true, 0, false);
                    _turnStartHighlightCells.Add(targetCell);
                }
            }
        }

        private void ClearTurnStartHighlights()
        {
            foreach (Vector2Int cell in _turnStartHighlightCells)
            {
                if (_tileMap.TryGetValue(cell, out TileView tile))
                {
                    tile.SetHover(false, 0, false);
                }
            }

            _turnStartHighlightCells.Clear();
        }

        private void OnEnemyDamaged(EnemyDamagedEvent e)
        {
            if (_gridManager == null)
            {
                Debug.LogError("[GridViewManager] _gridManager is null");
                return;
            }

            Vector3 worldPos = _gridManager.CellToWorld(e.EnemyPosition);
            SpawnDamagePopup(worldPos, e.Damage);
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

        private void ClearHoverOnly()
        {
            if (!_hasHoverCell) return;
            if (_tileMap.TryGetValue(_hoverCell, out TileView tile))
            {
                bool keepTurnStartHighlight = _turnStartHighlightCells.Contains(_hoverCell);
                tile.SetHover(keepTurnStartHighlight, 0, false);
            }
            _hasHoverCell = false;
        }

        private void ClearGhostOnly()
        {
            if (!_hasGhostCell) return;
            if (_tileMap.TryGetValue(_ghostCell, out TileView tile))
            {
                bool keepTurnStartHighlight = _turnStartHighlightCells.Contains(_ghostCell);
                tile.SetHover(keepTurnStartHighlight, 0, false);
            }
            _hasGhostCell = false;
        }

        private void OnWallDestroyed(WallDestroyedEvent e)
        {
            if (_tileMap.TryGetValue(e.Cell, out TileView tile))
            {
                tile.SetWall(false);
            }
        }
    }
}
