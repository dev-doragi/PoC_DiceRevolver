using UnityEngine;

namespace PocDiceTactics
{
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
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private CylinderSystem _cylinderSystem;
        [SerializeField] private WaveManager _waveManager;

        [Header("Player")]
        [SerializeField] private int _playerMaxHp = 10;

        private int _playerCurrentHp;
        private bool _isGameOver;
        private TurnPhase _currentPhase;
        private bool _isWaitingForVisual;

        public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerTurn && !_isGameOver;
        public GridManager GridManager => _gridManager;
        public PlayerController PlayerController => _playerController;
        public TurnPhase CurrentPhase => _currentPhase;
        public int RoundIndex => _waveManager != null ? _waveManager.RoundIndex : 0;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false; // Scene scope
            base.Awake();
        }

        protected override void OnBootstrap()
        {
            _playerCurrentHp = _playerMaxHp;
            _gridManager.GenerateGrid(); // Grid already generated in GridManager.OnBootstrap(), but ensure here

            Vector2Int playerCell = _gridManager.GetRandomEmptyCell();
            _playerController.Initialize(this, _gridManager, _cylinderSystem, playerCell);
            _cylinderSystem.Initialize(this, _gridManager);

            _waveManager.Initialize(this, _gridManager);
            _waveManager.SpawnNextWave();
            _waveManager.ExecuteEnemyTurnsAndTelegraphs();

            SetPhase(TurnPhase.PlayerTurn);
            EventBus.Instance?.Publish(new RoundStartedEvent { RoundIndex = 1 });
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<VisualSequenceCompleteEvent>(OnVisualSequenceComplete);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }

            EventBus.Instance.Unsubscribe<VisualSequenceCompleteEvent>(OnVisualSequenceComplete);
        }

        public void EndPlayerTurn()
        {
            if (!IsPlayerTurn) return;

            _isWaitingForVisual = true;
            SetPhase(TurnPhase.WaitingForVisual);
        }

        private void OnVisualSequenceComplete(VisualSequenceCompleteEvent _)
        {
            if (!_isWaitingForVisual || _isGameOver)
            {
                return;
            }

            _isWaitingForVisual = false;
            SetPhase(TurnPhase.EnemyTurn);
            EventBus.Instance?.Publish(new EnemyTurnStartedEvent());
            _waveManager.ExecuteEnemyTurnsAndTelegraphs();
            _gridManager.UpdateOverheat();

            if (_waveManager.AliveEnemyCount == 0)
            {
                StartNextRound();
            }
            else if (!_isGameOver)
            {
                SetPhase(TurnPhase.PlayerTurn);
                EventBus.Instance?.Publish(new PlayerTurnStartedEvent());
            }
        }

        private void StartNextRound()
        {
            SetPhase(TurnPhase.RoundTransition);
            EventBus.Instance?.Publish(new RoundClearedEvent { RoundIndex = _waveManager.RoundIndex });

            Vector2Int cachedPlayerPosition = _playerController.GridPosition;
            _gridManager.GenerateGrid(cachedPlayerPosition);
            _playerController.SetGridPosition(cachedPlayerPosition);
            _waveManager.SpawnNextWave();

            _waveManager.ExecuteEnemyTurnsAndTelegraphs();

            SetPhase(TurnPhase.PlayerTurn);
            EventBus.Instance?.Publish(new RoundStartedEvent { RoundIndex = _waveManager.RoundIndex });
        }

        public void DamagePlayer(int damage)
        {
            if (_isGameOver) return;

            _playerCurrentHp -= damage;
            EventBus.Instance?.Publish(new PlayerDamagedEvent { Damage = damage, RemainingHp = _playerCurrentHp });

            Debug.Log($"[TurnManager] Player HP: {_playerCurrentHp}");

            if (_playerCurrentHp <= 0)
            {
                _playerCurrentHp = 0;
                _isGameOver = true;
                _waveManager.ClearAllEnemies();
                SetPhase(TurnPhase.GameOver);
                EventBus.Instance?.Publish(new PlayerDiedEvent { Position = _playerController != null ? _playerController.GridPosition : Vector2Int.zero });
                EventBus.Instance?.Publish(new GameOverEvent());
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ChangeState(GameState.GameOver);
                }
                Debug.Log("[TurnManager] Game Over");
            }
        }

        private void SetPhase(TurnPhase phase)
        {
            _currentPhase = phase;
            EventBus.Instance?.Publish(new PhaseChangedEvent { Phase = _currentPhase });
        }
    }
}
