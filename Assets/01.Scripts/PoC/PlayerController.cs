using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using DG.Tweening;

namespace PocDiceTactics
{
    /// <summary>
    /// 주사위 이동 및 방향 변경을 처리합니다.
    /// </summary>
    public class PlayerController : Singleton<PlayerController>
    {
        [Header("2D View")]
        [SerializeField] private float _moveLerpSpeed = 16f;
        [SerializeField] private LayerMask _tileLayerMask = ~0;
        [SerializeField] private SpriteRenderer[] _deathFadeRenderers;
        [SerializeField] private float _deathEffectDuration = 0.35f;
        [SerializeField] private float _bumpDistance = 0.18f;
        [SerializeField] private float _bumpDuration = 0.14f;
        [SerializeField] private float _recoilDistance = 0.14f;
        [SerializeField] private float _recoilDuration = 0.12f;

        private TurnManager _turnManager;
        private GridManager _gridManager;
        private CylinderSystem _cylinderSystem;

        private Vector2Int _gridPosition;
        private Vector2Int _facing = Vector2Int.up;

        private int _topFace = 1;
        private int _bottomFace = 6;
        private int _northFace = 2;
        private int _southFace = 5;
        private int _eastFace = 3;
        private int _westFace = 4;

        private Vector2Int _pendingMoveDirection;
        private bool _hasPendingMove;
        private Vector3 _moveTargetWorld;
        private Camera _mainCamera;
        private bool _isDead;

        public Vector2Int GridPosition => _gridPosition;
        public Vector2Int Facing => _facing;
        public int TopFace => _topFace;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false; // Scene scope
            base.Awake();
        }

        protected override void OnBootstrap()
        {
            // No specific bootstrap logic needed, initialized via TurnManager
            _pendingMoveDirection = Vector2Int.zero;
            _hasPendingMove = false;
            _mainCamera = Camera.main;

            // PoC 입력 이벤트 구독
            EventBus.Instance.Subscribe<MoveUpPressedEvent>(OnMoveUpPressed);
            EventBus.Instance.Subscribe<MoveDownPressedEvent>(OnMoveDownPressed);
            EventBus.Instance.Subscribe<MoveLeftPressedEvent>(OnMoveLeftPressed);
            EventBus.Instance.Subscribe<MoveRightPressedEvent>(OnMoveRightPressed);
            EventBus.Instance.Subscribe<FirePressedEvent>(OnFirePressed);
            EventBus.Instance.Subscribe<ClickEvent>(OnClick);
            EventBus.Instance.Subscribe<RightClickEvent>(OnRightClick);
            EventBus.Instance.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        public void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }

            // PoC 입력 이벤트 구독 해제
            EventBus.Instance.Unsubscribe<MoveUpPressedEvent>(OnMoveUpPressed);
            EventBus.Instance.Unsubscribe<MoveDownPressedEvent>(OnMoveDownPressed);
            EventBus.Instance.Unsubscribe<MoveLeftPressedEvent>(OnMoveLeftPressed);
            EventBus.Instance.Unsubscribe<MoveRightPressedEvent>(OnMoveRightPressed);
            EventBus.Instance.Unsubscribe<FirePressedEvent>(OnFirePressed);
            EventBus.Instance.Unsubscribe<ClickEvent>(OnClick);
            EventBus.Instance.Unsubscribe<RightClickEvent>(OnRightClick);
            EventBus.Instance.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        public void Initialize(TurnManager turnManager, GridManager gridManager, CylinderSystem cylinderSystem, Vector2Int startCell)
        {
            _turnManager = turnManager;
            _gridManager = gridManager;
            _cylinderSystem = cylinderSystem;

            _gridPosition = startCell;
            _gridManager.RegisterOccupant(_gridPosition, this);
            _moveTargetWorld = _gridManager.CellToWorld(_gridPosition);
            transform.position = _moveTargetWorld;
            _isDead = false;
            SetDeathVisualAlpha(1f);
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _moveTargetWorld, Time.deltaTime * _moveLerpSpeed);

            if (_isDead) return;
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;

            PublishHoverGhost();
        }

        private void HandleMoveInput(Vector2Int direction)
        {
            if (!_hasPendingMove || _pendingMoveDirection != direction)
            {
                _pendingMoveDirection = direction;
                _hasPendingMove = true;

                int predictedTopFace = PredictTopFace(direction);
                EventBus.Instance?.Publish(new MoveGhostEvent
                {
                    TargetCell = _gridPosition + direction,
                    PredictedTopFace = predictedTopFace,
                    IsConfirmRequired = true
                });
                return;
            }

            _hasPendingMove = false;
            TryMove(direction);
        }

        private int PredictTopFace(Vector2Int direction)
        {
            int previousTop = _topFace;
            int previousNorth = _northFace;
            int previousSouth = _southFace;
            int previousEast = _eastFace;
            int previousWest = _westFace;

            if (direction == Vector2Int.up) return previousSouth;
            if (direction == Vector2Int.down) return previousNorth;
            if (direction == Vector2Int.right) return previousWest;
            if (direction == Vector2Int.left) return previousEast;
            return previousTop;
        }

        private void PublishHoverGhost()
        {
            if (Camera.main == null)
            {
                return;
            }

            if (!TryGetMouseTargetCell(out Vector2Int hoverCell))
            {
                return;
            }

            Vector2Int delta = hoverCell - _gridPosition;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return;
            }

            int predictedTopFace = PredictTopFace(delta);
            EventBus.Instance?.Publish(new TileHoverEvent
            {
                Cell = hoverCell,
                PredictedTopFace = predictedTopFace
            });
        }

        private bool TryGetMouseTargetCell(out Vector2Int targetCell)
        {
            targetCell = _gridPosition;
            if (InputReader.Instance == null)
            {
                return false;
            }

            if (InputReader.Instance.IsPointerOverUI)
            {
                return false;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return false;
            }

            Vector2 mousePosition = InputReader.Instance.GetMousePosition();

            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, _tileLayerMask);
            if (hit.collider == null)
            {
                return false;
            }

            TileView tileView = hit.collider.GetComponent<TileView>();
            if (tileView != null)
            {
                targetCell = tileView.Cell;
            }
            else
            {
                targetCell = _gridManager.WorldToCell(hit.point);
            }

            if (!_gridManager.IsInside(targetCell))
            {
                return false;
            }

            return true;
        }

        private bool TryGetAdjacentDirectionFromMouse(out Vector2Int direction)
        {
            direction = Vector2Int.zero;

            if (!TryGetMouseTargetCell(out Vector2Int clickCell))
            {
                return false;
            }

            Vector2Int delta = clickCell - _gridPosition;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            direction = delta;
            return true;
        }

        private void TryMove(Vector2Int direction)
        {
            Vector2Int nextCell = _gridPosition + direction;
            if (!_gridManager.TryMoveOccupant(_gridPosition, nextCell, this))
            {
                PlayBump(direction);
                return;
            }

            _gridPosition = nextCell;
            _facing = direction;
            _moveTargetWorld = _gridManager.CellToWorld(_gridPosition);

            RollDice(direction);
            bool isOverheated = _gridManager.IsOverheated(_gridPosition);
            _cylinderSystem.OnPlayerMoved(_topFace, isOverheated);

            EventBus.Instance?.Publish(new PlayerMovedEvent { NewPosition = _gridPosition, Facing = _facing, TopFace = _topFace });

            _turnManager.EndPlayerTurn();
        }

        public void SetGridPosition(Vector2Int position)
        {
            if (_gridManager != null)
            {
                _gridManager.UnregisterOccupant(_gridPosition, this);
            }

            _gridPosition = position;
            _gridManager.RegisterOccupant(_gridPosition, this);
            _moveTargetWorld = _gridManager.CellToWorld(_gridPosition);
            transform.position = _moveTargetWorld;
            _hasPendingMove = false;
            _pendingMoveDirection = Vector2Int.zero;
        }

        private void RollDice(Vector2Int direction)
        {
            int previousTop = _topFace;
            int previousBottom = _bottomFace;
            int previousNorth = _northFace;
            int previousSouth = _southFace;
            int previousEast = _eastFace;
            int previousWest = _westFace;

            if (direction == Vector2Int.up)
            {
                _topFace = previousSouth;
                _bottomFace = previousNorth;
                _northFace = previousTop;
                _southFace = previousBottom;
            }
            else if (direction == Vector2Int.down)
            {
                _topFace = previousNorth;
                _bottomFace = previousSouth;
                _northFace = previousBottom;
                _southFace = previousTop;
            }
            else if (direction == Vector2Int.right)
            {
                _topFace = previousWest;
                _bottomFace = previousEast;
                _eastFace = previousTop;
                _westFace = previousBottom;
            }
            else if (direction == Vector2Int.left)
            {
                _topFace = previousEast;
                _bottomFace = previousWest;
                _eastFace = previousBottom;
                _westFace = previousTop;
            }
        }

        private void OnMoveUpPressed(MoveUpPressedEvent e)
        {
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;
            HandleMoveInput(Vector2Int.up);
        }

        private void OnMoveDownPressed(MoveDownPressedEvent e)
        {
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;
            HandleMoveInput(Vector2Int.down);
        }

        private void OnMoveLeftPressed(MoveLeftPressedEvent e)
        {
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;
            HandleMoveInput(Vector2Int.left);
        }

        private void OnMoveRightPressed(MoveRightPressedEvent _)
        {
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;
            HandleMoveInput(Vector2Int.right);
        }

        private void OnFirePressed(FirePressedEvent e)
        {
            if (_isDead) return;
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;
            TryFire();
        }

        private void TryFire()
        {
            if (_cylinderSystem != null)
            {
                _cylinderSystem.Fire(_gridPosition, _facing);
                PlayRecoil(_facing);
                _turnManager.EndPlayerTurn();
            }
        }

        private void OnRightClick(RightClickEvent e)
        {
            if (_isDead) return;
            if (!e.IsStarted) return;
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;

            if (TryGetAdjacentDirectionFromMouse(out Vector2Int clickDirection))
            {
                _hasPendingMove = false;
                _pendingMoveDirection = Vector2Int.zero;
                TryMove(clickDirection);
            }
        }

        private void OnClick(ClickEvent e)
        {
            if (_isDead) return;
            if (!e.IsStarted) return;
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;

            TryFireAtMouseTarget();
        }

        private void TryFireAtMouseTarget()
        {
            if (_cylinderSystem == null) return;
            if (!TryGetMouseTargetCell(out Vector2Int targetCell)) return;

            Vector2Int delta = targetCell - _gridPosition;
            if (delta == Vector2Int.zero) return;

            _facing = delta;
            _cylinderSystem.Fire(_gridPosition, delta);
            PlayRecoil(delta);
            _turnManager.EndPlayerTurn();
        }

        private void PlayBump(Vector2Int direction)
        {
            transform.DOKill();

            Vector3 punch = new Vector3(direction.x, direction.y, 0f) * _bumpDistance;
            transform.DOPunchPosition(punch, _bumpDuration, 1, 0f)
                .SetEase(Ease.OutQuad);
        }

        private void PlayRecoil(Vector2Int fireDirection)
        {
            transform.DOKill();

            Vector2 recoilDirection = new Vector2(-fireDirection.x, -fireDirection.y);
            if (recoilDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            recoilDirection.Normalize();
            Vector3 recoilOffset = new Vector3(recoilDirection.x, recoilDirection.y, 0f) * _recoilDistance;
            Vector3 origin = _moveTargetWorld;

            transform.DOMove(origin + recoilOffset, _recoilDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    transform.DOMove(origin, _recoilDuration * 0.5f)
                        .SetEase(Ease.InQuad);
                });
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (_isDead) return;
            _isDead = true;
            StartCoroutine(PlayDeathEffectRoutine());
        }

        private IEnumerator PlayDeathEffectRoutine()
        {
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = startScale * 0.7f;

            while (elapsed < _deathEffectDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _deathEffectDuration);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                SetDeathVisualAlpha(Mathf.Lerp(1f, 0.2f, t));
                yield return null;
            }
        }

        private void SetDeathVisualAlpha(float alpha)
        {
            if (_deathFadeRenderers == null) return;

            for (int i = 0; i < _deathFadeRenderers.Length; i++)
            {
                SpriteRenderer renderer = _deathFadeRenderers[i];
                if (renderer == null) continue;

                Color c = renderer.color;
                c.a = alpha;
                renderer.color = c;
            }
        }
    }
}