using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using DG.Tweening;
using TMPro;

namespace PocDiceTactics
{
    /// <summary>
    /// 주사위 이동 및 방향 변경을 처리합니다.
    /// </summary>
    public class PlayerController : Singleton<PlayerController>
    {
        [Header("Stats")]
        [SerializeField] private int _maxHp = 10;

        [Header("2D View")]
        [SerializeField] private float _moveLerpSpeed = 16f;
        [SerializeField] private LayerMask _tileLayerMask = ~0;
        [SerializeField] private SpriteRenderer[] _deathFadeRenderers;
        [SerializeField] private float _deathEffectDuration = 0.35f;
        [SerializeField] private float _bumpDistance = 0.18f;
        [SerializeField] private float _bumpDuration = 0.14f;
        [SerializeField] private float _recoilDistance = 0.14f;
        [SerializeField] private float _recoilDuration = 0.12f;
        [SerializeField] private float _moveVisualDuration = 0.18f;
        [SerializeField] private TextMeshPro _topFaceText;

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
        private bool _isMoveVisualPlaying;
        private bool _isFiringVisualPlaying;
        private Coroutine _gatlingRoutine;
        private int _currentHp;
        public int CurrentAP { get; private set; }
        public int CurrentHP { get; private set; }

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
            EventBus.Instance.Subscribe<CylinderDryFiredEvent>(OnCylinderDryFired);
            EventBus.Instance.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
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
            EventBus.Instance.Unsubscribe<CylinderDryFiredEvent>(OnCylinderDryFired);
            EventBus.Instance.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
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
            CurrentHP = _maxHp;
            CurrentAP = 2;
            EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });
            EventBus.Instance?.Publish(new PlayerDamagedEvent { Damage = 0, RemainingHp = _currentHp });
            SetDeathVisualAlpha(1f);
            transform.localScale = Vector3.one;
            UpdateTopFaceText();
        }

        private void Update()
        {
            if (!_isMoveVisualPlaying)
            {
                transform.position = Vector3.Lerp(transform.position, _moveTargetWorld, Time.deltaTime * _moveLerpSpeed);
            }

            if (_isDead) return;
            if (_turnManager == null || !_turnManager.IsPlayerTurn) return;

            PublishHoverGhost();
        }

        private void HandleMoveInput(Vector2Int direction)
        {
            if (CurrentAP <= 0)
            {
                return;
            }

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
            if (CurrentAP <= 0)
            {
                return;
            }

            Vector2Int nextCell = _gridPosition + direction;
            if (!_gridManager.TryMoveOccupant(_gridPosition, nextCell, this))
            {
                PlayBump(direction);
                return;
            }

            int nextTopFace = PredictTopFace(direction);
            _gridPosition = nextCell;
            _facing = direction;
            _moveTargetWorld = _gridManager.CellToWorld(_gridPosition);

            RollDice(direction);
            bool isOverheated = _gridManager.IsOverheated(_gridPosition);
            _cylinderSystem.OnPlayerMoved(_topFace, isOverheated);

            EventBus.Instance?.Publish(new PlayerMovedEvent { NewPosition = _gridPosition, Facing = _facing, TopFace = _topFace });

            if (_turnManager == null)
            {
                Debug.LogError("[PlayerController] _turnManager is null");
                return;
            }

            ConsumeAPAndCheckTurn(true);
            PlayMoveVisualSequence(direction, nextTopFace);
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
            UpdateTopFaceText();
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
            if (CurrentAP <= 0)
            {
                return;
            }

            if (_cylinderSystem != null)
            {
                if (_isFiringVisualPlaying)
                {
                    return;
                }

                if (GameModeManager.Instance != null && GameModeManager.Instance.CurrentMode == DiceMode.Gatling)
                {
                    if (_gatlingRoutine != null)
                    {
                        StopCoroutine(_gatlingRoutine);
                    }

                    _gatlingRoutine = StartCoroutine(FireGatlingRoutine(_facing));
                }
                else
                {
                    _cylinderSystem.Fire(_gridPosition, _facing);
                    ConsumeAPAndCheckTurn(false);
                    PlayRecoil(_facing);
                }
            }
            else
            {
                Debug.LogError("[PlayerController] _cylinderSystem is null");
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
            if (CurrentAP <= 0)
            {
                return;
            }

            if (_isFiringVisualPlaying)
            {
                return;
            }

            if (_cylinderSystem == null)
            {
                Debug.LogError("[PlayerController] _cylinderSystem is null");
                return;
            }
            if (!TryGetMouseTargetCell(out Vector2Int targetCell)) return;

            Vector2Int delta = targetCell - _gridPosition;
            if (delta == Vector2Int.zero) return;

            _facing = delta;
            if (GameModeManager.Instance != null && GameModeManager.Instance.CurrentMode == DiceMode.Gatling)
            {
                if (_gatlingRoutine != null)
                {
                    StopCoroutine(_gatlingRoutine);
                }

                _gatlingRoutine = StartCoroutine(FireGatlingRoutine(delta));
            }
            else
            {
                _cylinderSystem.Fire(_gridPosition, delta);
                ConsumeAPAndCheckTurn(false);
                PlayRecoil(delta);
            }
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (evt.Phase != TurnPhase.PlayerTurn)
            {
                return;
            }

            if (GameModeManager.Instance != null && GameModeManager.Instance.CurrentMode == DiceMode.APPumping)
            {
                CurrentAP = _topFace;
            }
            else
            {
                CurrentAP = 2;
            }
            EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });
        }

        private IEnumerator FireGatlingRoutine(Vector2Int direction)
        {
            int shots = _topFace;
            for (int i = 0; i < shots; i++)
            {
                bool success = _cylinderSystem.TryPeekAndFire(_gridPosition, direction);
                if (!success)
                {
                    EventBus.Instance?.Publish(new CylinderDryFiredEvent { ChamberIndex = -1 });
                    break;
                }

                PlayRecoil(direction);
                yield return new WaitForSeconds(0.15f);
            }

            _cylinderSystem.ConsumeCurrentChamberAndRotate();
            ConsumeAPAndCheckTurn(false);
            _gatlingRoutine = null;
        }

        private void ConsumeAPAndCheckTurn(bool isMovement)
        {
            CurrentAP--;
            EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });

            if (_turnManager == null)
            {
                Debug.LogError("[PlayerController] _turnManager is null");
                return;
            }

            if (CurrentAP <= 0)
            {
                _turnManager.EndPlayerTurn();
            }
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
            _isFiringVisualPlaying = true;
            if (InputReader.Instance != null)
            {
                InputReader.Instance.SetInputBlocked(true);
            }

            transform.DOKill();

            Vector2 recoilDirection = new Vector2(-fireDirection.x, -fireDirection.y);
            if (recoilDirection.sqrMagnitude < 0.0001f)
            {
                _isFiringVisualPlaying = false;
                if (InputReader.Instance != null)
                {
                    InputReader.Instance.SetInputBlocked(false);
                }
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
                        .SetEase(Ease.InQuad)
                        .OnComplete(() =>
                        {
                            _isFiringVisualPlaying = false;
                            if (InputReader.Instance != null)
                            {
                                InputReader.Instance.SetInputBlocked(false);
                            }

                            if (_turnManager != null && CurrentAP <= 0)
                            {
                                _turnManager.OnVisualsCompleted();
                            }
                        });
                });
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (_isDead) return;
            _isDead = true;
            StartCoroutine(PlayDeathEffectRoutine());
        }

        private void OnCylinderDryFired(CylinderDryFiredEvent _)
        {
            if (_turnManager == null)
            {
                Debug.LogError("[PlayerController] _turnManager is null");
                return;
            }

            if (_turnManager.CurrentPhase == TurnPhase.WaitingForVisual)
            {
                _turnManager.OnVisualsCompleted();
            }
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

        private void PlayMoveVisualSequence(Vector2Int direction, int nextTopFace)
        {
            transform.DOKill();
            _isMoveVisualPlaying = true;

            float duration = Mathf.Max(0.02f, _moveVisualDuration);
            float half = duration * 0.5f;
            bool isHorizontal = direction.x != 0;

            Sequence sequence = DOTween.Sequence();
            sequence.Join(transform.DOMove(_moveTargetWorld, duration).SetEase(Ease.OutQuad));
            sequence.Join(DOVirtual.Float(0f, 1f, half, t =>
            {
                ApplyMoveScale(isHorizontal, t, true);
            }));

            sequence.AppendCallback(() =>
            {
                _topFace = nextTopFace;
                UpdateTopFaceText();
            });

            sequence.Append(DOVirtual.Float(0f, 1f, half, t =>
            {
                ApplyMoveScale(isHorizontal, t, false);
            }));

            sequence.OnComplete(() =>
            {
                transform.position = _moveTargetWorld;
                transform.localScale = Vector3.one;
                _isMoveVisualPlaying = false;

                if (_turnManager != null && CurrentAP <= 0)
                {
                    _turnManager.OnVisualsCompleted();
                }
            });
        }

        public void TakeDamage(int damage)
        {
            if (_isDead)
            {
                return;
            }

            _currentHp -= damage;
            EventBus.Instance?.Publish(new PlayerDamagedEvent { Damage = damage, RemainingHp = _currentHp });

            if (_currentHp <= 0)
            {
                _currentHp = 0;
                _isDead = true;
                EventBus.Instance?.Publish(new PlayerDiedEvent { Position = _gridPosition });
                EventBus.Instance?.Publish(new GameOverEvent());
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeState(GameState.GameOver);
                }
            }
        }

        public void ForceEndTurnFromOverload()
        {
            CurrentAP = 0;
            EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });

            if (_turnManager == null)
            {
                Debug.LogError("[PlayerController] _turnManager is null");
                return;
            }

            _turnManager.EndPlayerTurn();
        }

        private void ApplyMoveScale(bool isHorizontal, float t, bool firstHalf)
        {
            float baseScale = firstHalf ? Mathf.Lerp(1f, 1.2f, t) : Mathf.Lerp(1.2f, 1f, t);
            float flipScale = firstHalf ? Mathf.Lerp(1f, 0f, t) : Mathf.Lerp(0f, 1f, t);

            Vector3 targetScale;
            if (isHorizontal)
            {
                targetScale = new Vector3(flipScale, baseScale, 1f);
            }
            else
            {
                targetScale = new Vector3(baseScale, flipScale, 1f);
            }

            transform.localScale = targetScale;
        }

        private void UpdateTopFaceText()
        {
            if (_topFaceText == null)
            {
                Debug.LogError("[PlayerController] _topFaceText is null");
                return;
            }

            _topFaceText.text = _topFace.ToString();
        }
    }
}