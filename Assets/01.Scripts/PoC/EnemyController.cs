using UnityEngine;

namespace PocDiceTactics
{
    /// <summary>
    /// 적의 추적 및 공격을 담당하는 컨트롤러입니다.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private int _maxHp = 2;
        [SerializeField] private int _attackDamage = 1;

        private int _currentHp;
        private int _shackleRemainingTurns;

        private TurnManager _turnManager;
        private GridManager _gridManager;
        private WaveManager _waveManager;

        private Vector2Int _gridPosition;
        private bool _hasTelegraph;
        private Vector2Int _telegraphTargetCell;

        public Vector2Int GridPosition => _gridPosition;

        public void Initialize(TurnManager turnManager, GridManager gridManager, WaveManager waveManager, Vector2Int spawnCell)
        {
            _turnManager = turnManager;
            _gridManager = gridManager;
            _waveManager = waveManager;

            _currentHp = _maxHp;
            _gridPosition = spawnCell;
            _hasTelegraph = false;
            _telegraphTargetCell = Vector2Int.zero;

            _gridManager.RegisterOccupant(_gridPosition, this);
            transform.position = _gridManager.CellToWorld(_gridPosition);
        }

        public void ExecuteTelegraph(Vector2Int playerPos)
        {
            if (_shackleRemainingTurns > 0)
            {
                ClearTelegraphIfNeeded();
                _hasTelegraph = false;
                return;
            }

            int distance = Mathf.Abs(playerPos.x - _gridPosition.x) + Mathf.Abs(playerPos.y - _gridPosition.y);
            if (distance == 1)
            {
                _hasTelegraph = true;
                _telegraphTargetCell = playerPos;
                EventBus.Instance?.Publish(new EnemyTelegraphEvent
                {
                    EnemyPosition = _gridPosition,
                    TargetCell = _telegraphTargetCell,
                    IsActive = true
                });
                return;
            }

            ClearTelegraphIfNeeded();
            _hasTelegraph = false;
        }

        public void ExecuteAction(Vector2Int playerPos)
        {
            if (_shackleRemainingTurns > 0)
            {
                _shackleRemainingTurns--;
                ClearTelegraphIfNeeded();
                _hasTelegraph = false;
                return;
            }

            if (_hasTelegraph)
            {
                if (_telegraphTargetCell == playerPos)
                {
                    _turnManager.DamagePlayer(_attackDamage);
                    EventBus.Instance?.Publish(new EnemyAttackedEvent { EnemyPosition = _gridPosition });
                }

                EventBus.Instance?.Publish(new EnemyTelegraphEvent
                {
                    EnemyPosition = _gridPosition,
                    TargetCell = _telegraphTargetCell,
                    IsActive = false
                });

                _hasTelegraph = false;
                return;
            }

            TryMoveTowardPlayer(playerPos);
        }

        private void TryMoveTowardPlayer(Vector2Int playerPos)
        {
            Vector2Int[] candidates =
            {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };

            Vector2Int bestCell = _gridPosition;
            int bestDistance = int.MaxValue;

            foreach (Vector2Int dir in candidates)
            {
                Vector2Int next = _gridPosition + dir;
                if (!_gridManager.IsWalkable(next, this)) continue;

                int distance = Mathf.Abs(playerPos.x - next.x) + Mathf.Abs(playerPos.y - next.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = next;
                }
            }

            if (bestCell != _gridPosition)
            {
                _gridManager.TryMoveOccupant(_gridPosition, bestCell, this);
                _gridPosition = bestCell;
                transform.position = _gridManager.CellToWorld(_gridPosition);
                EventBus.Instance?.Publish(new EnemyMovedEvent { EnemyPosition = _gridPosition });
            }
        }

        public bool TryPush(Vector2Int direction)
        {
            Vector2Int next = _gridPosition + direction;
            if (!_gridManager.IsWalkable(next, this)) return false;

            _gridManager.TryMoveOccupant(_gridPosition, next, this);
            _gridPosition = next;
            transform.position = _gridManager.CellToWorld(_gridPosition);
            return true;
        }

        public void ApplyShackle(int turns)
        {
            ClearTelegraphIfNeeded();
            _shackleRemainingTurns = Mathf.Max(_shackleRemainingTurns, turns);
        }

        public void TakeDamage(int damage)
        {
            _currentHp -= damage;
            if (_currentHp <= 0)
            {
                Die();
            }
            else
            {
                EventBus.Instance?.Publish(new EnemyDamagedEvent { EnemyPosition = _gridPosition, Damage = damage });
            }
        }

        private void Die()
        {
            ClearTelegraphIfNeeded();
            _gridManager.UnregisterOccupant(_gridPosition, this);
            _waveManager.NotifyEnemyDead(this);
            EventBus.Instance?.Publish(new EnemyDiedEvent { EnemyPosition = _gridPosition });
            Destroy(gameObject);
        }

        private void ClearTelegraphIfNeeded()
        {
            if (!_hasTelegraph)
            {
                return;
            }

            EventBus.Instance?.Publish(new EnemyTelegraphEvent
            {
                EnemyPosition = _gridPosition,
                TargetCell = _telegraphTargetCell,
                IsActive = false
            });
        }
    }
}