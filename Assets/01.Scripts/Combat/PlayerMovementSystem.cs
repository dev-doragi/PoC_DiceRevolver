using UnityEngine;

public class PlayerMovementSystem : MonoBehaviour
{
    private PlayerManager _playerManager;
    private PlayerController _playerController;
    private TurnManager _turnManager;
    private GridManager _gridManager;

    private Vector2Int _pendingMoveDirection;
    private bool _hasPendingMove;

    public void Initialize(PlayerManager playerManager)
    {
        _playerManager = playerManager;
        _playerController = GetComponent<PlayerController>();
        _pendingMoveDirection = Vector2Int.zero;
        _hasPendingMove = false;
    }

    public void InitializeCombatContext(TurnManager turnManager, GridManager gridManager, Vector2Int startCell)
    {
        _turnManager = turnManager;
        _gridManager = gridManager;

        if (_playerController == null)
        {
            Debug.LogError("[PlayerMovementSystem] _playerController is null");
            return;
        }

        _playerController.SetGridPositionInternal(startCell);
        _playerController.SetMoveTargetWorld(_gridManager.CellToWorld(startCell)); // 추가

        if (!_gridManager.RegisterOccupant(_playerController.GridPosition, _playerController))
        {
            Debug.LogError("[PlayerMovementSystem] Failed to register player occupant");
        }

        _pendingMoveDirection = Vector2Int.zero;
        _hasPendingMove = false;
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("[PlayerMovementSystem] EventBus.Instance is null");
            return;
        }

        EventBus.Instance.Subscribe<MoveUpPressedEvent>(OnMoveUpPressed);
        EventBus.Instance.Subscribe<MoveDownPressedEvent>(OnMoveDownPressed);
        EventBus.Instance.Subscribe<MoveLeftPressedEvent>(OnMoveLeftPressed);
        EventBus.Instance.Subscribe<MoveRightPressedEvent>(OnMoveRightPressed);
        EventBus.Instance.Subscribe<RightClickEvent>(OnRightClick);
        EventBus.Instance.Subscribe<OnVisualsCompletedEvent>(OnVisualsCompleted);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Unsubscribe<MoveUpPressedEvent>(OnMoveUpPressed);
        EventBus.Instance.Unsubscribe<MoveDownPressedEvent>(OnMoveDownPressed);
        EventBus.Instance.Unsubscribe<MoveLeftPressedEvent>(OnMoveLeftPressed);
        EventBus.Instance.Unsubscribe<MoveRightPressedEvent>(OnMoveRightPressed);
        EventBus.Instance.Unsubscribe<RightClickEvent>(OnRightClick);
        EventBus.Instance.Unsubscribe<OnVisualsCompletedEvent>(OnVisualsCompleted);
    }

    private void OnMoveUpPressed(MoveUpPressedEvent _)
    {
        HandleMoveInput(Vector2Int.up);
    }

    private void OnMoveDownPressed(MoveDownPressedEvent _)
    {
        HandleMoveInput(Vector2Int.down);
    }

    private void OnMoveLeftPressed(MoveLeftPressedEvent _)
    {
        HandleMoveInput(Vector2Int.left);
    }

    private void OnMoveRightPressed(MoveRightPressedEvent _)
    {
        HandleMoveInput(Vector2Int.right);
    }

    private void OnRightClick(RightClickEvent evt)
    {
        if (!evt.IsStarted)
        {
            return;
        }

        if (!CanProcessMovement())
        {
            return;
        }

        if (!_playerController.TryGetAdjacentDirectionFromMouse(out Vector2Int direction))
        {
            return;
        }

        _hasPendingMove = false;
        _pendingMoveDirection = Vector2Int.zero;
        TryMove(direction);
    }

    private void HandleMoveInput(Vector2Int direction)
    {
        if (!CanProcessMovement())
        {
            return;
        }

        if (_playerManager == null || _playerManager.Status == null)
        {
            Debug.LogError("[PlayerMovementSystem] PlayerStatusSystem is null");
            return;
        }

        if (_playerManager.Status.CurrentAP <= 0)
        {
            return;
        }

        if (!_hasPendingMove || _pendingMoveDirection != direction)
        {
            _pendingMoveDirection = direction;
            _hasPendingMove = true;

            int predictedTopFace = _playerController.PredictTopFace(direction);
            EventBus.Instance.Publish(new MoveGhostEvent
            {
                TargetCell = _playerController.GridPosition + direction,
                PredictedTopFace = predictedTopFace,
                IsConfirmRequired = true
            });
            return;
        }

        _hasPendingMove = false;
        TryMove(direction);
    }

    public bool TryMove(Vector2 input)
    {
        Vector2Int direction = Vector2Int.zero;
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            direction = input.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else if (Mathf.Abs(input.y) > 0f)
        {
            direction = input.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        if (direction == Vector2Int.zero)
        {
            return false;
        }

        return TryMove(direction);
    }

    private bool TryMove(Vector2Int direction)
    {
        if (!CanProcessMovement())
        {
            return false;
        }

        Vector2Int nextCell = _playerController.GridPosition + direction;
        bool isEnteringOverheated = _gridManager.IsOverheated(nextCell);

        if (!_gridManager.TryMoveOccupant(_playerController.GridPosition, nextCell, _playerController))
        {
            _playerController.PlayBump(direction);
            return false;
        }

        int nextTopFace = _playerController.PredictTopFace(direction);

        _playerController.SetFacing(direction);
        _playerController.RollDice(direction);
        _playerController.SetGridPositionInternal(nextCell);
        _playerController.SetMoveTargetWorld(_gridManager.CellToWorld(nextCell));

        if (_playerManager.Combat != null)
        {
            _playerManager.Combat.OnPlayerMoved(_playerController.TopFace, isEnteringOverheated);
        }

        EventBus.Instance.Publish(new PlayerMovedEvent
        {
            NewPosition = _playerController.GridPosition,
            Facing = _playerController.Facing,
            TopFace = _playerController.TopFace
        });

        _playerManager.Status.ConsumeAP();
        _playerController.PlayMoveVisualSequence(direction, nextTopFace);
        return true;
    }

    private void OnVisualsCompleted(OnVisualsCompletedEvent _)
    {
        if (_turnManager == null)
        {
            Debug.LogError("[PlayerMovementSystem] _turnManager is null");
            return;
        }

        _turnManager.OnVisualsCompleted();
    }

    private bool CanProcessMovement()
    {
        if (_playerController == null || _turnManager == null || _gridManager == null)
        {
            return false;
        }

        if (_playerController.IsDead)
        {
            return false;
        }

        if (!_turnManager.IsPlayerTurn)
        {
            return false;
        }

        return true;
    }
}
