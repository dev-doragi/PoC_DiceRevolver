using System.Collections.Generic;
using UnityEngine;

namespace PocDiceTactics
{
    /// <summary>
    /// 적 스폰과 라운드 관리를 담당합니다.
    /// </summary>
    public class WaveManager : Singleton<WaveManager>
    {
        [SerializeField] private EnemyController _enemyPrefab;
        [SerializeField] private int _enemiesPerWave = 3;

        private readonly List<EnemyController> _aliveEnemies = new List<EnemyController>();

        private TurnManager _turnManager;
        private GridManager _gridManager;
        private int _roundIndex;

        public int AliveEnemyCount => _aliveEnemies.Count;
        public int RoundIndex => _roundIndex;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false; // Scene scope
            base.Awake();
        }

        protected override void OnBootstrap()
        {
            // No specific bootstrap logic needed, initialized via TurnManager
        }

        public void Initialize(TurnManager turnManager, GridManager gridManager)
        {
            _turnManager = turnManager;
            _gridManager = gridManager;
        }

        public void SpawnNextWave()
        {
            _roundIndex++;
            Debug.Log($"[WaveManager] Round {_roundIndex} Start");

            // 기존 적 제거 (없어야 함)
            foreach (EnemyController enemy in _aliveEnemies.ToArray())
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
            _aliveEnemies.Clear();

            // 새 적 스폰
            List<Vector2Int> spawnCells = GetRandomSpawnCells(_enemiesPerWave);
            foreach (Vector2Int cell in spawnCells)
            {
                EnemyController enemy = Instantiate(_enemyPrefab, _gridManager.CellToWorld(cell), Quaternion.identity);
                enemy.Initialize(_turnManager, _gridManager, this, cell);
                _aliveEnemies.Add(enemy);
            }

            EventBus.Instance?.Publish(new EnemiesSpawnedEvent { SpawnPositions = spawnCells });
        }

        private List<Vector2Int> GetRandomSpawnCells(int count)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int y = 0; y < _gridManager.GridSize.y; y++)
            {
                for (int x = 0; x < _gridManager.GridSize.x; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (_gridManager.IsWalkable(cell) && cell != _turnManager.PlayerController.GridPosition)
                    {
                        candidates.Add(cell);
                    }
                }
            }

            List<Vector2Int> selected = new List<Vector2Int>();
            for (int i = 0; i < Mathf.Min(count, candidates.Count); i++)
            {
                int index = Random.Range(0, candidates.Count);
                selected.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return selected;
        }

        public void ExecuteEnemyTurns()
        {
            EnemyController[] enemies = _aliveEnemies.ToArray();
            for (int i = enemies.Length - 1; i >= 0; i--)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.ExecuteAction(_turnManager.PlayerController.GridPosition);
            }

            _aliveEnemies.RemoveAll(enemy => enemy == null);
        }

        public void ExecuteEnemyTurnsAndTelegraphs()
        {
            Vector2Int playerPos = _turnManager.PlayerController.GridPosition;

            EnemyController[] enemies = _aliveEnemies.ToArray();
            for (int i = enemies.Length - 1; i >= 0; i--)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.ExecuteAction(playerPos);
                if (enemy != null)
                {
                    enemy.ExecuteTelegraph(_turnManager.PlayerController.GridPosition);
                }
            }

            _aliveEnemies.RemoveAll(enemy => enemy == null);
        }

        public void NotifyEnemyDead(EnemyController enemy)
        {
            _aliveEnemies.Remove(enemy);
        }

        public void ClearAllEnemies()
        {
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = _aliveEnemies[i];
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            _aliveEnemies.Clear();
        }
    }
}