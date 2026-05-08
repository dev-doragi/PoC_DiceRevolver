using UnityEngine;
using System.Collections;

namespace PocDiceTactics
{
    public enum TurnPhase
    {
        PlayerInput,
        ActionResolve,
        EnemyTelegraph,
        EnemyAction,
        Cleanup,
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

        [Header("Turn Timing")]
        [SerializeField] private float _phaseIntervalSeconds = 0.25f;
        [SerializeField] private float _roundTransitionSeconds = 1f;

        private int _playerCurrentHp;
        private bool _isGameOver;
        private TurnPhase _currentPhase;

        public bool IsPlayerTurn => _currentPhase == TurnPhase.PlayerInput && !_isGameOver;
        public GridManager GridManager => _gridManager;
        public PlayerController PlayerController => _playerController;
        public TurnPhase CurrentPhase => _currentPhase;

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

            SetPhase(TurnPhase.PlayerInput);
            EventBus.Instance?.Publish(new RoundStartedEvent { RoundIndex = 1 });
        }

        public void EndPlayerTurn()
        {
            if (!IsPlayerTurn) return;

            StartCoroutine(TurnLoopRoutine());
        }

        private IEnumerator TurnLoopRoutine()
        {
            SetPhase(TurnPhase.ActionResolve);
            yield return new WaitForSeconds(_phaseIntervalSeconds);

            SetPhase(TurnPhase.EnemyTelegraph);
            _waveManager.ExecuteEnemyTelegraphs();
            yield return new WaitForSeconds(_phaseIntervalSeconds);

            SetPhase(TurnPhase.EnemyAction);
            EventBus.Instance?.Publish(new EnemyTurnStartedEvent());
            _waveManager.ExecuteEnemyTurns();
            yield return new WaitForSeconds(_phaseIntervalSeconds);

            SetPhase(TurnPhase.Cleanup);
            _gridManager.UpdateOverheat();
            yield return new WaitForSeconds(_phaseIntervalSeconds);

            if (_waveManager.AliveEnemyCount == 0)
            {
                StartCoroutine(NextRoundRoutine());
            }
            else if (!_isGameOver)
            {
                SetPhase(TurnPhase.PlayerInput);
                EventBus.Instance?.Publish(new PlayerTurnStartedEvent());
            }
        }

        private IEnumerator NextRoundRoutine()
        {
            SetPhase(TurnPhase.RoundTransition);
            EventBus.Instance?.Publish(new RoundClearedEvent { RoundIndex = _waveManager.RoundIndex });

            yield return new WaitForSeconds(_roundTransitionSeconds);

            Vector2Int cachedPlayerPosition = _playerController.GridPosition;
            _gridManager.GenerateGrid(cachedPlayerPosition);
            _playerController.SetGridPosition(cachedPlayerPosition);
            _waveManager.SpawnNextWave();

            SetPhase(TurnPhase.PlayerInput);
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
