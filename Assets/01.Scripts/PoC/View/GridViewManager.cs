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

        private readonly Dictionary<Vector2Int, TileView> _tileMap = new Dictionary<Vector2Int, TileView>();
        private readonly HashSet<Vector2Int> _telegraphCells = new HashSet<Vector2Int>();
        private Coroutine _shotRoutine;

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

            _shotRoutine = StartCoroutine(PlayShotPathRoutine(e));
        }

        private IEnumerator PlayShotPathRoutine(ShotFiredEvent e)
        {
            Color shotColor = GetShotColor(e.BulletType);
            Vector2Int current = e.Origin;

            while (true)
            {
                current += e.Direction;
                if (_gridManager == null || !_gridManager.IsInside(current))
                {
                    break;
                }

                if (_tileMap.TryGetValue(current, out TileView tile))
                {
                    tile.PlayShotFlash(shotColor);
                }

                if (_gridManager.IsWall(current) && e.BulletType != 6)
                {
                    break;
                }

                if (e.BulletType != 6)
                {
                    if (_gridManager.GetOccupant(current) is EnemyController)
                    {
                        break;
                    }
                }

                yield return new WaitForSeconds(_shotStepInterval);
            }

            _shotRoutine = null;
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