using System.Collections;
using UnityEngine;

public class PlayerCombatSystem : MonoBehaviour
{
    private PlayerManager _playerManager;
    private PlayerController _playerController;
    private TurnManager _turnManager;
    private GridManager _gridManager;
    private CylinderSystem _cylinderSystem;

    private Coroutine _gatlingRoutine;

    public void Initialize(PlayerManager playerManager)
    {
        _playerManager = playerManager;
        _playerController = GetComponent<PlayerController>();
    }

    public void InitializeCombatContext(TurnManager turnManager, GridManager gridManager, CylinderSystem cylinderSystem)
    {
        _turnManager = turnManager;
        _gridManager = gridManager;
        _cylinderSystem = cylinderSystem;
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("[PlayerCombatSystem] EventBus.Instance is null");
            return;
        }

        EventBus.Instance.Subscribe<FirePressedEvent>(OnFirePressed);
        EventBus.Instance.Subscribe<ClickEvent>(OnClick);
        EventBus.Instance.Subscribe<CylinderDryFiredEvent>(OnCylinderDryFired);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Unsubscribe<FirePressedEvent>(OnFirePressed);
        EventBus.Instance.Unsubscribe<ClickEvent>(OnClick);
        EventBus.Instance.Unsubscribe<CylinderDryFiredEvent>(OnCylinderDryFired);
    }

    private void OnFirePressed(FirePressedEvent _)
    {
        if (!CanProcessCombat())
        {
            return;
        }

        if (!TryResolveFireDirection(out Vector2Int direction))
        {
            return;
        }

        TryFire(direction);
    }

    private void OnClick(ClickEvent evt)
    {
        if (!evt.IsStarted)
        {
            return;
        }

        if (InputReader.Instance != null && InputReader.Instance.IsPointerOverUI)
        {
            return;
        }

        if (!CanProcessCombat())
        {
            return;
        }

        if (!TryResolveFireDirection(out Vector2Int direction))
        {
            return;
        }

        TryFire(direction);
    }

    private bool TryResolveFireDirection(out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        if (_playerController == null)
        {
            Debug.LogError("[PlayerCombatSystem] _playerController is null");
            return false;
        }

        if (_playerController.TryGetCurrentHoverDirection(out _, out Vector2Int hoverDirection))
        {
            if (hoverDirection == Vector2Int.zero)
            {
                Debug.LogError("[PlayerCombatSystem] direction is zero in TryResolveFireDirection");
                return false;
            }

            _playerController.SetFacing(hoverDirection);
            direction = hoverDirection;
            return true;
        }

        if (_playerController.Facing == Vector2Int.zero)
        {
            Debug.LogError("[PlayerCombatSystem] direction is zero in TryResolveFireDirection");
            return false;
        }

        direction = _playerController.Facing;
        return true;
    }

    private void TryFire(Vector2Int direction)
    {
        if (_playerManager == null || _playerManager.Status == null)
        {
            Debug.LogError("[PlayerCombatSystem] PlayerStatusSystem is null");
            return;
        }

        if (direction == Vector2Int.zero)
        {
            Debug.LogError("[PlayerCombatSystem] direction is zero in TryFire");
            return;
        }

        if (_playerManager.Status.CurrentAP <= 0)
        {
            return;
        }

        if (_cylinderSystem == null)
        {
            Debug.LogError("[PlayerCombatSystem] _cylinderSystem is null");
            return;
        }

        if (_playerController.IsFiringVisualPlaying)
        {
            return;
        }

        // 영거리 벽 사격 방지
        BulletLogicSO currentBullet = _cylinderSystem.GetCurrentBulletLogic();

        if (currentBullet == null || !currentBullet.CanIgnoreMuzzleBlock)
        {
            var muzzleResult = PathCalculationService.CalculateLaserPath(
                _playerController.GridPosition,
                new Vector2(direction.x, direction.y),
                2, // 짧은 거리로 첫 번째 타일 확인
                _gridManager.IsWall,
                _gridManager.IsInside
            );

            // 인접 칸이 벽이면 PassedTiles는 비고 HitWall만 true가 됨
            bool isImmediateWallBlock = muzzleResult.HitWall && (muzzleResult.PassedTiles == null || muzzleResult.PassedTiles.Count == 0);
            if (isImmediateWallBlock)
            {
                EventBus.Instance?.Publish(new CylinderDryFiredEvent { ChamberIndex = -1 });
                return;
            }
        }

        if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.Gatling)
        {
            if (_gatlingRoutine != null)
            {
                StopCoroutine(_gatlingRoutine);
            }

            _gatlingRoutine = StartCoroutine(FireGatlingRoutine(direction));
            return;
        }

        if (_cylinderSystem.Fire(_playerController.GridPosition, direction, out ShotFiredEvent shotEvent))
        {
            EventBus.Instance?.Publish(shotEvent);
        }

        _playerManager.Status.ConsumeAP();
        _playerController.PlayRecoil(direction);
    }

    public void OnPlayerMoved(int topFace, bool isEnteringOverheated)
    {
        if (_cylinderSystem == null)
        {
            Debug.LogError("[PlayerCombatSystem] _cylinderSystem is null");
            return;
        }

        _cylinderSystem.OnPlayerMoved(topFace, isEnteringOverheated);
    }

    private IEnumerator FireGatlingRoutine(Vector2Int direction)
    {
        int shots = _playerController.TopFace;
        for (int i = 0; i < shots; i++)
        {
            bool success = _cylinderSystem.TryPeekAndFire(_playerController.GridPosition, direction);
            if (!success)
            {
                EventBus.Instance.Publish(new CylinderDryFiredEvent { ChamberIndex = -1 });
                break;
            }

            _playerController.PlayRecoil(direction);
            yield return new WaitForSeconds(0.15f);
        }

        _cylinderSystem.ConsumeCurrentChamberAndRotate();
        _playerManager.Status.ConsumeAP();
        _gatlingRoutine = null;
    }

    private void OnCylinderDryFired(CylinderDryFiredEvent _)
    {
        EventBus.Instance?.Publish(new OnVisualsCompletedEvent());
    }

    private bool CanProcessCombat()
    {
        if (_playerController == null || _turnManager == null)
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
