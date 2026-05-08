using UnityEngine;
using System.Collections.Generic;

namespace PocDiceTactics
{
    /// <summary>
    /// 주사위 굴림, 실린더 회전, 사격 로직이 포함된 플레이어 시스템입니다.
    /// </summary>
    public class CylinderSystem : Singleton<CylinderSystem>
    {
        [Header("Damage Settings")]
        [SerializeField] private int _normalDamage = 1;
        [SerializeField] private int _knockbackDamage = 1;
        [SerializeField] private int _wallCollisionDamage = 1;
        [SerializeField] private int _pierceDamage = 1;
        [SerializeField] private int _ricochetDamage = 1;
        [SerializeField] private int _shackleDamage = 1;
        [SerializeField] private int _magnumDamage = 3;
        [SerializeField] private int _shackleTurns = 2;

        private readonly int?[] _chambers = new int?[6];
        private int _firePointer = 0;
        private int _loadPointer = 3; // (0 + 3) % 6

        private TurnManager _turnManager;
        private GridManager _gridManager;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false; // Scene scope
            base.Awake();
        }

        protected override void OnBootstrap()
        {
            // No specific bootstrap logic needed
        }

        public void Initialize(TurnManager turnManager, GridManager gridManager)
        {
            _turnManager = turnManager;
            _gridManager = gridManager;
            
            PublishStateSnapshot(); // 초기 상태 전달
        }

        public void OnPlayerMoved(int topFace, bool isOverheated)
        {
            RotateCylinder();

            if (!isOverheated && !_chambers[_loadPointer].HasValue)
            {
                _chambers[_loadPointer] = Mathf.Clamp(topFace, 1, 6);
                EventBus.Instance?.Publish(new CylinderLoadedEvent { ChamberIndex = _loadPointer, BulletType = _chambers[_loadPointer].Value });
            }

            PublishStateSnapshot(); // 행동 완료 후 스냅샷 전달
        }

        public void Fire(Vector2Int origin, Vector2Int direction)
        {
            int? loadedBullet = _chambers[_firePointer];

            if (loadedBullet.HasValue)
            {
                ResolveBullet(loadedBullet.Value, origin, direction);
                EventBus.Instance?.Publish(new ShotFiredEvent
                {
                    Origin = origin,
                    Direction = direction,
                    BulletType = loadedBullet.Value
                });
                _chambers[_firePointer] = null;
                EventBus.Instance?.Publish(new CylinderFiredEvent { ChamberIndex = _firePointer, BulletType = loadedBullet.Value });
            }
            else
            {
                // Dry Fire
                EventBus.Instance?.Publish(new CylinderDryFiredEvent { ChamberIndex = _firePointer });
            }
            PublishStateSnapshot(); // 행동 완료 후 스냅샷 전달
        }

        private void RotateCylinder()
        {
            _firePointer = (_firePointer + 1) % _chambers.Length;
            _loadPointer = (_loadPointer + 1) % _chambers.Length;
            EventBus.Instance?.Publish(new CylinderRotatedEvent { NewFirePointer = _firePointer, NewLoadPointer = _loadPointer });
        }

        private void ResolveBullet(int bulletType, Vector2Int origin, Vector2Int direction)
        {
            switch (bulletType)
            {
                case 1:
                case 2:
                    FireNormal(origin, direction);
                    break;
                case 3:
                case 4:
                    FireMagnum(origin, direction);
                    break;
                case 5:
                    FireShackle(origin, direction);
                    break;
                case 6:
                    FirePiercing(origin, direction);
                    break;
            }
        }

        private void FireNormal(Vector2Int origin, Vector2Int direction)
        {
            EnemyController enemy = GetFirstEnemyInLine(origin, direction, 1);
            if (enemy != null) enemy.TakeDamage(_normalDamage);
        }

        private void FireKnockback(Vector2Int origin, Vector2Int direction)
        {
            EnemyController enemy = GetFirstEnemyInLine(origin, direction, 1);
            if (enemy == null) return;

            enemy.TakeDamage(_knockbackDamage);
            bool pushed = enemy.TryPush(direction);

            if (!pushed)
            {
                enemy.TakeDamage(_wallCollisionDamage);
            }
        }

        private void FirePiercing(Vector2Int origin, Vector2Int direction)
        {
            foreach (EnemyController enemy in GetEnemiesInLine(origin, direction))
            {
                enemy.TakeDamage(_pierceDamage);
            }
        }

        private void FireRicochet(Vector2Int origin, Vector2Int direction)
        {
            Vector2Int current = origin;
            Vector2Int rayDir = direction;
            int bounceCount = 0;

            while (true)
            {
                Vector2Int next = current + rayDir;
                if (!_gridManager.IsInside(next)) break;

                if (_gridManager.IsWall(next))
                {
                    bounceCount++;
                    if (bounceCount > 1) break;

                    rayDir = new Vector2Int(-rayDir.x, -rayDir.y);
                    continue;
                }

                current = next;
                EnemyController enemy = _gridManager.GetOccupant(current) as EnemyController;
                if (enemy != null)
                {
                    enemy.TakeDamage(_ricochetDamage);
                    break;
                }
            }
        }

        private void FireShackle(Vector2Int origin, Vector2Int direction)
        {
            EnemyController enemy = GetFirstEnemyInLine(origin, direction, 1);
            if (enemy == null) return;

            enemy.TakeDamage(_shackleDamage);
            enemy.ApplyShackle(_shackleTurns);
        }

        private void FireMagnum(Vector2Int origin, Vector2Int direction)
        {
            EnemyController enemy = GetFirstEnemyInLine(origin, direction, 1);
            if (enemy != null) enemy.TakeDamage(_magnumDamage);
        }

        private EnemyController GetFirstEnemyInLine(Vector2Int origin, Vector2Int direction, int maxDistance)
        {
            Vector2Int current = origin;

            for (int i = 0; i < maxDistance; i++)
            {
                current += direction;
                if (!_gridManager.IsInside(current)) return null;
                if (_gridManager.IsWall(current)) return null;

                EnemyController enemy = _gridManager.GetOccupant(current) as EnemyController;
                if (enemy != null) return enemy;
            }

            return null;
        }

        private List<EnemyController> GetEnemiesInLine(Vector2Int origin, Vector2Int direction)
        {
            List<EnemyController> result = new List<EnemyController>();
            Vector2Int current = origin;

            while (true)
            {
                current += direction;
                if (!_gridManager.IsInside(current)) break;
                if (_gridManager.IsWall(current)) break;

                EnemyController enemy = _gridManager.GetOccupant(current) as EnemyController;
                if (enemy != null) result.Add(enemy);
            }

            return result;
        }

        private void PublishStateSnapshot()
        {
            // 이벤트 참조 꼬임 방지를 위한 배열 복제
            int?[] clonedChambers = new int?[6];
            System.Array.Copy(_chambers, clonedChambers, 6);

            EventBus.Instance?.Publish(new CylinderStateChangedEvent
            {
                Chambers = clonedChambers,
                FirePointer = _firePointer,
                LoadPointer = _loadPointer
            });
        }
    }
}