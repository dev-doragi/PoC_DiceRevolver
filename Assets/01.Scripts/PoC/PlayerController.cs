using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class PlayerController : MonoBehaviour
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
    [SerializeField] private float _moveVisualDuration = 0.18f;
    [SerializeField] private TextMeshPro _topFaceText;

    private Camera _mainCamera;
    private Vector3 _moveTargetWorld;
    private Vector2Int _gridPosition;
    private Vector2Int _facing = Vector2Int.up;

    private int _topFace = 1;
    private int _bottomFace = 6;
    private int _northFace = 2;
    private int _southFace = 5;
    private int _eastFace = 3;
    private int _westFace = 4;

    private bool _isDead;
    private bool _isMoveVisualPlaying;
    private bool _isFiringVisualPlaying;

    public Vector2Int GridPosition => _gridPosition;
    public Vector2Int Facing => _facing;
    public int TopFace => _topFace;
    public bool IsDead => _isDead;
    public bool IsFiringVisualPlaying => _isFiringVisualPlaying;

    private void Awake()
    {
        _mainCamera = Camera.main;

        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[PlayerController] PlayerManager.Instance is null");
            throw new System.InvalidOperationException("[PlayerController] PlayerManager.Instance is null");
        }

        PlayerManager.Instance.RegisterPlayer(this);
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("[PlayerController] EventBus.Instance is null");
            return;
        }

        EventBus.Instance.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Instance.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Instance.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
    }

    private void Update()
    {
        if (!_isMoveVisualPlaying)
        {
            transform.position = Vector3.Lerp(transform.position, _moveTargetWorld, Time.deltaTime * _moveLerpSpeed);
        }

        if (_isDead)
        {
            return;
        }

        UpdateFacingFromMouseHover();

        PublishHoverGhost();
    }

    private void OnPhaseChanged(PhaseChangedEvent e)
    {
        if (e.Phase != TurnPhase.PlayerTurn)
        {
            return;
        }

        _facing = Vector2Int.zero;
        UpdateFacingFromMouseHover();
    }

    private void UpdateFacingFromMouseHover()
    {
        if (TurnManager.Instance == null || !TurnManager.Instance.IsPlayerTurn)
        {
            return;
        }

        if (!GetCurrentAimDirection(out Vector2Int direction))
        {
            return;
        }

        _facing = direction;
    }

    public int PredictTopFace(Vector2Int direction)
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

    public void RollDice(Vector2Int direction)
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

        UpdateTopFaceText();
    }

    public void SetFacing(Vector2Int direction)
    {
        _facing = direction;
    }

    public void SetGridPositionInternal(Vector2Int position)
    {
        _gridPosition = position;
    }

    public void SetMoveTargetWorld(Vector3 worldPosition)
    {
        _moveTargetWorld = worldPosition;
        transform.position = worldPosition;
    }

    public bool TryGetMouseTargetCell(out Vector2Int targetCell)
    {
        targetCell = _gridPosition;

        GridManager gridManager = TurnManager.Instance != null ? TurnManager.Instance.GridManager : null;
        if (gridManager == null)
        {
            Debug.LogError("[PlayerController] GridManager is null");
            return false;
        }

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
            if (_mainCamera == null)
            {
                Debug.LogError("[PlayerController] Camera.main is null");
                return false;
            }
        }

        Vector2 mousePosition = InputReader.Instance.GetMousePosition();
        Vector3 mouseWorld = _mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, 0f));
        targetCell = gridManager.WorldToCell(mouseWorld);

        if (!gridManager.IsInside(targetCell))
        {
            return false;
        }

        return true;
    }

    public bool GetCurrentAimDirection(out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        return TryResolveHoverAndAim(out _, out _, out _, out direction, out _);
    }

    public bool TryGetCurrentHoverDirection(out Vector2Int targetCell, out Vector2Int direction)
    {
        return TryResolveHoverAndAim(out targetCell, out _, out _, out direction, out _);
    }

    private bool TryResolveHoverAndAim(out Vector2Int targetCell, out bool hasAdjacentHover, out Vector2Int adjacentDelta, out Vector2Int aimDirection, out int predictedTopFace)
    {
        targetCell = _gridPosition;
        hasAdjacentHover = false;
        adjacentDelta = Vector2Int.zero;
        aimDirection = Vector2Int.zero;
        predictedTopFace = 0;

        if (!TryGetMouseTargetCell(out Vector2Int currentTargetCell))
        {
            return false;
        }

        targetCell = currentTargetCell;

        Vector2Int adjacent = targetCell - _gridPosition;
        if (Mathf.Abs(adjacent.x) + Mathf.Abs(adjacent.y) == 1)
        {
            hasAdjacentHover = true;
            adjacentDelta = adjacent;
            predictedTopFace = PredictTopFace(adjacentDelta);
        }

        Vector2Int delta = targetCell - _gridPosition;
        if (delta == Vector2Int.zero)
        {
            return false;
        }

        int absX = Mathf.Abs(delta.x);
        int absY = Mathf.Abs(delta.y);
        int gcd = GreatestCommonDivisor(absX, absY);
        if (gcd <= 0)
        {
            return false;
        }

        aimDirection = new Vector2Int(delta.x / gcd, delta.y / gcd);
        return true;
    }

    private int GreatestCommonDivisor(int a, int b)
    {
        if (a == 0) return b;
        if (b == 0) return a;

        while (b != 0)
        {
            int temp = a % b;
            a = b;
            b = temp;
        }

        return Mathf.Abs(a);
    }

    public bool TryGetAdjacentDirectionFromMouse(out Vector2Int direction)
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

    public void PlayBump(Vector2Int direction)
    {
        transform.DOKill();

        Vector3 punch = new Vector3(direction.x, direction.y, 0f) * _bumpDistance;
        transform.DOPunchPosition(punch, _bumpDuration, 1, 0f).SetEase(Ease.OutQuad);
    }

    public void PlayRecoil(Vector2Int fireDirection)
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

            EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
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

                        EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
                    });
            });
    }

    public void PlayMoveVisualSequence(Vector2Int direction, int nextTopFace)
    {
        transform.DOKill();
        _isMoveVisualPlaying = true;

        float duration = Mathf.Max(0.02f, _moveVisualDuration);
        float half = duration * 0.5f;
        bool isHorizontal = direction.x != 0;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(transform.DOMove(_moveTargetWorld, duration).SetEase(Ease.OutQuad));
        sequence.Join(DOVirtual.Float(0f, 1f, half, t => { ApplyMoveScale(isHorizontal, t, true); }));

        sequence.AppendCallback(() =>
        {
            _topFace = nextTopFace;
            UpdateTopFaceText();
        });

        sequence.Append(DOVirtual.Float(0f, 1f, half, t => { ApplyMoveScale(isHorizontal, t, false); }));

        sequence.OnComplete(() =>
        {
            transform.position = _moveTargetWorld;
            transform.localScale = Vector3.one;
            _isMoveVisualPlaying = false;

            EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
        });
    }

    private void PublishHoverGhost()
    {
        bool hasAim = TryResolveHoverAndAim(out Vector2Int targetCell, out bool hasAdjacentHover, out _, out Vector2Int aimDirection, out int predictedTopFace);
        Vector2Int hoverCell = hasAdjacentHover ? targetCell : _gridPosition;

        if (hasAdjacentHover)
        {
            EventBus.Instance?.Publish(new TileHoverEvent
            {
                Cell = hoverCell,
                PredictedTopFace = predictedTopFace
            });
        }
        else
        {
            EventBus.Instance?.Publish(new TileHoverEvent
            {
                Cell = _gridPosition,
                PredictedTopFace = 0
            });
        }

        if (!hasAim)
        {
            EventBus.Instance?.Publish(new RicochetTrajectoryPreviewEvent
            {
                IsActive = false,
                Origin = _gridPosition,
                Direction = Vector2Int.zero,
                LogicResult = default
            });
            return;
        }

        if (CylinderSystem.Instance == null)
        {
            Debug.LogError("[PlayerController] CylinderSystem.Instance is null");
            EventBus.Instance?.Publish(new RicochetTrajectoryPreviewEvent
            {
                IsActive = false,
                Origin = _gridPosition,
                Direction = Vector2Int.zero,
                LogicResult = default
            });
            return;
        }

        if (CylinderSystem.Instance.TryGetRicochetPreviewPath(_gridPosition, aimDirection, out GridManager.LaserLogicResult logicResult))
        {
            EventBus.Instance?.Publish(new RicochetTrajectoryPreviewEvent
            {
                IsActive = true,
                Origin = _gridPosition,
                Direction = aimDirection,
                LogicResult = logicResult
            });
            return;
        }

        EventBus.Instance?.Publish(new RicochetTrajectoryPreviewEvent
        {
            IsActive = false,
            Origin = _gridPosition,
            Direction = Vector2Int.zero,
            LogicResult = default
        });
    }

    private void OnPlayerDied(PlayerDiedEvent _)
    {
        if (_isDead)
        {
            return;
        }

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
        if (_deathFadeRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _deathFadeRenderers.Length; i++)
        {
            SpriteRenderer renderer = _deathFadeRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }
    }

    public void SetDead(bool dead)
    {
        _isDead = dead;
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
