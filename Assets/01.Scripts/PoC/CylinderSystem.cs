using UnityEngine;
using System.Collections.Generic;

namespace PocDiceTactics
{
    /// <summary>
    /// 주사위 굴림, 실린더 회전, 사격 로직이 포함된 플레이어 시스템입니다.
    /// </summary>
    public class CylinderSystem : Singleton<CylinderSystem>
    {
        [Header("Bullet Database")]
        [SerializeField] private BulletLogicSO[] _bulletDatabase = new BulletLogicSO[6];

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
                _chambers[_loadPointer] = Mathf.Clamp(topFace - 1, 0, 5);
                EventBus.Instance?.Publish(new CylinderLoadedEvent { ChamberIndex = _loadPointer, BulletType = _chambers[_loadPointer].Value });
            }

            PublishStateSnapshot(); // 행동 완료 후 스냅샷 전달
        }

        public void Fire(Vector2Int origin, Vector2Int direction)
        {
            int? loadedBullet = _chambers[_firePointer];
            List<Vector3> pathPoints = new List<Vector3>();

            if (loadedBullet.HasValue)
            {
                int bulletType = loadedBullet.Value;
                if (bulletType >= 0 && bulletType < _bulletDatabase.Length && _bulletDatabase[bulletType] != null)
                {
                    pathPoints = _bulletDatabase[bulletType].Execute(origin, direction, _gridManager);
                }
                else
                {
                    Debug.LogError($"[CylinderSystem] Invalid bullet type or missing SO: {bulletType}");
                }

                EventBus.Instance?.Publish(new ShotFiredEvent
                {
                    Origin = origin,
                    Direction = direction,
                    BulletType = bulletType,
                    PathPoints = pathPoints
                });
                _chambers[_firePointer] = null;
                EventBus.Instance?.Publish(new CylinderFiredEvent { ChamberIndex = _firePointer, BulletType = bulletType });
            }
            else
            {
                // Dry Fire
                EventBus.Instance?.Publish(new CylinderDryFiredEvent { ChamberIndex = _firePointer });
            }

            RotateCylinder();
            PublishStateSnapshot(); // 행동 완료 후 스냅샷 전달
        }

        private void RotateCylinder()
        {
            _firePointer = (_firePointer - 1 + _chambers.Length) % _chambers.Length;
            _loadPointer = (_firePointer + 4) % _chambers.Length;

            EventBus.Instance?.Publish(new CylinderRotatedEvent { NewFirePointer = _firePointer, NewLoadPointer = _loadPointer });
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