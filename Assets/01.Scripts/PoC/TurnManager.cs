using UnityEngine;

public enum TurnPhase
{
    PlayerTurn,
    WaitingForVisual,
    EnemyTurn,
    RoundTransition,
    GameOver
}

/// <summary>
/// 플레이어와 적의 턴을 관리하는 상태 머신입니다.
/// </summary>
public class TurnManager : Singleton<TurnManager>
{
    [Header("References")]
    [SerializeField] private GridManager _gridManager;
    [SerializeField] private CylinderSystem _cylinderSystem;
    [SerializeField] private WaveManager _waveManager;

    private bool _isGameOver;
    private TurnPhase _currentPhase;
    private bool _isWaitingForVisual;

    public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerTurn && !_isGameOver;
    public GridManager GridManager => _gridManager;
    public TurnPhase CurrentPhase => _currentPhase;
    public int RoundIndex => _waveManager != null ? _waveManager.RoundIndex : 0;
    public Vector2Int PlayerGridPosition => PlayerManager.Instance != null && PlayerManager.Instance.PlayerTransform != null
        ? GridManager.WorldToCell(PlayerManager.Instance.PlayerTransform.position)
        : Vector2Int.zero;

    protected override void Awake()
    {
        _isDontDestroyOnLoad = false; // Scene scope
        base.Awake();
    }

    protected override void OnBootstrap()
    {
        if (PlayerManager.Instance == null)
        {
            Debug.LogError("[TurnManager] PlayerManager.Instance is null");
            throw new System.InvalidOperationException("[TurnManager] PlayerManager.Instance is null");
        }

        PlayerManager.Instance.BootstrapIfNeeded();

        if (_gridManager == null)
        {
            Debug.LogError("[TurnManager] _gridManager is null");
            throw new System.InvalidOperationException("[TurnManager] _gridManager is null");
        }

        if (_waveManager == null)
        {
            Debug.LogError("[TurnManager] _waveManager is null");
            throw new System.InvalidOperationException("[TurnManager] _waveManager is null");
        }

        if (_cylinderSystem == null)
        {
            Debug.LogError("[TurnManager] _cylinderSystem is null");
            throw new System.InvalidOperationException("[TurnManager] _cylinderSystem is null");
        }

        _waveManager.Initialize(this, _gridManager);
        _gridManager.GenerateGrid();

        Vector2Int playerCell = _gridManager.GetRandomEmptyCell();
        PlayerManager.Instance.InitializeCombatContext(this, _gridManager, _cylinderSystem, playerCell);
        _cylinderSystem.Initialize(this, _gridManager);

        _waveManager.SpawnNextWave();

        SetPhase(TurnPhase.PlayerTurn);
        EventBus.Instance?.Publish(new RoundStartedEvent { RoundIndex = 1 });
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            Debug.LogError("[TurnManager] EventBus.Instance is null");
            return;
        }

        EventBus.Instance.Subscribe<PlayerTurnEndedEvent>(OnPlayerTurnEnded);
        EventBus.Instance.Subscribe<OnVisualsCompletedEvent>(OnVisualsCompletedEvent);
        EventBus.Instance.Subscribe<EnemyTurnCompletedEvent>(OnEnemyTurnCompleted);
        EventBus.Instance.Subscribe<AllEnemiesDefeatedEvent>(OnAllEnemiesDefeated);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Unsubscribe<PlayerTurnEndedEvent>(OnPlayerTurnEnded);
        EventBus.Instance.Unsubscribe<OnVisualsCompletedEvent>(OnVisualsCompletedEvent);
        EventBus.Instance.Unsubscribe<EnemyTurnCompletedEvent>(OnEnemyTurnCompleted);
        EventBus.Instance.Unsubscribe<AllEnemiesDefeatedEvent>(OnAllEnemiesDefeated);
    }

    private void OnPlayerTurnEnded(PlayerTurnEndedEvent _)
    {
        EndPlayerTurn();
    }

    private void OnVisualsCompletedEvent(OnVisualsCompletedEvent _)
    {
        OnVisualsCompleted();
    }

    public void EndPlayerTurn()
    {
        if (!IsPlayerTurn) return;

        _isWaitingForVisual = true;
        SetPhase(TurnPhase.WaitingForVisual);
    }

    public void OnVisualsCompleted()
    {
        if (_isGameOver)
        {
            return;
        }

        if (_currentPhase != TurnPhase.WaitingForVisual || !_isWaitingForVisual)
        {
            return;
        }

        _isWaitingForVisual = false;
        SetPhase(TurnPhase.EnemyTurn);
        EventBus.Instance?.Publish(new StartEnemyTurnEvent());
    }

    private void StartNextRound()
    {
        SetPhase(TurnPhase.RoundTransition);
        EventBus.Instance?.Publish(new RoundClearedEvent { RoundIndex = _waveManager.RoundIndex });

        Vector2Int cachedPlayerPosition = PlayerGridPosition;
        _gridManager.GenerateGrid(cachedPlayerPosition);
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.InitializeCombatContext(this, _gridManager, _cylinderSystem, cachedPlayerPosition);
        }
        _waveManager.SpawnNextWave();
        if (PlayerManager.Instance != null && PlayerManager.Instance.Status != null)
        {
            PlayerManager.Instance.Status.ResetAP();
        }

        SetPhase(TurnPhase.PlayerTurn);
        EventBus.Instance?.Publish(new RoundStartedEvent { RoundIndex = _waveManager.RoundIndex });
    }

    private void SetPhase(TurnPhase phase)
    {
        _currentPhase = phase;
        EventBus.Instance?.Publish(new PhaseChangedEvent { Phase = _currentPhase });
    }

    private void OnEnemyTurnCompleted(EnemyTurnCompletedEvent e)
    {
        if (_isGameOver)
        {
            return;
        }

        SetPhase(TurnPhase.PlayerTurn);
        EventBus.Instance?.Publish(new PlayerTurnStartedEvent());
    }

    private void OnAllEnemiesDefeated(AllEnemiesDefeatedEvent e)
    {
        StartNextRound();
    }
}
