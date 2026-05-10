using UnityEngine;

public class PlayerStatusSystem : MonoBehaviour
{
    [SerializeField] private int _maxHp = 10;
    [SerializeField] private int _maxAP = 2;

    private PlayerManager _playerManager;
    private PlayerController _playerController;
    private TurnManager _turnManager;

    private int _currentHp;
    private int _currentAP;

    public int CurrentAP { get => _currentAP; private set => _currentAP = value; }
    public int CurrentHP => _currentHp;

    public void Initialize(PlayerManager playerManager)
    {
        _playerManager = playerManager;
        _playerController = GetComponent<PlayerController>();
        _currentHp = _maxHp;
        CurrentAP = _maxAP;
    }

    public void InitializeCombatContext(TurnManager turnManager)
    {
        _turnManager = turnManager;
        _currentHp = _maxHp;
        CurrentAP = _maxAP;

        EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });
        EventBus.Instance?.Publish(new PlayerDamagedEvent { Damage = 0, RemainingHp = _currentHp });
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("[PlayerStatusSystem] EventBus.Instance is null");
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

    private void OnPlayerDied(PlayerDiedEvent _)
    {
        _playerController.SetDead(true);
    }

    private void OnPhaseChanged(PhaseChangedEvent evt)
    {
        if (evt.Phase != TurnPhase.PlayerTurn)
        {
            return;
        }

        if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.APPumping)
        {
            CurrentAP = _playerController.TopFace;
        }
        else
        {
            CurrentAP = _maxAP;
        }

        EventBus.Instance.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });
    }

    public void ConsumeAP()
    {
        CurrentAP--;
        EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });

        if (CurrentAP <= 0)
        {
            EventBus.Instance?.Publish(new PlayerTurnEndedEvent());
        }
    }

    public void TakeDamage(int damage)
    {
        if (_playerController.IsDead)
        {
            return;
        }

        _currentHp -= damage;
        if (_currentHp <= 0)
        {
            _currentHp = 0;
        }

        EventBus.Instance?.Publish(new PlayerDamagedEvent { Damage = damage, RemainingHp = _currentHp });

        if (_currentHp <= 0)
        {
            _playerController.SetDead(true);
            EventBus.Instance?.Publish(new PlayerDiedEvent { Position = _playerController.GridPosition });
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
        EventBus.Instance?.Publish(new PlayerTurnEndedEvent());
    }

    public void ResetAP()
    {
        if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.APPumping)
        {
            CurrentAP = _playerController.TopFace;
        }
        else
        {
            CurrentAP = 2;
        }
        EventBus.Instance?.Publish(new PlayerAPChangedEvent { CurrentAP = CurrentAP });
    }
}
