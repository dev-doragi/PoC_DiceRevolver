using UnityEngine;
using System.Collections.Generic;

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
        private int _blackjackSum = 0;

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
            if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.BlackjackCylinder)
            {
                if (isOverheated)
                {
                    return;
                }

                _blackjackSum += topFace;
                if (_blackjackSum > 6)
                {
                    _blackjackSum = 0;
                    _chambers[_firePointer] = null;

                    if (PlayerManager.Instance != null && PlayerManager.Instance.Status != null)
                    {
                        PlayerManager.Instance.Status.TakeDamage(1);
                        PlayerManager.Instance.Status.ForceEndTurnFromOverload();

                        if (PoolManager.Instance != null && PlayerManager.Instance.PlayerTransform != null)
                        {
                            PoolManager.Instance.Spawn("Explosion", PlayerManager.Instance.PlayerTransform.position, Quaternion.identity);
                        }
                    }
                    else
                    {
                        Debug.LogError("[CylinderSystem] PlayerManager.Instance or Status is null");
                    }
                }
                else
                {
                    _chambers[_firePointer] = _blackjackSum - 1;
                }

                PublishStateSnapshot();
                return;
            }

            RotateCylinder();

            if (!isOverheated && !_chambers[_loadPointer].HasValue)
            {
                if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.RussianRoulette)
                {
                    if (Random.Range(0, 6) == 0)
                    {
                        _chambers[_loadPointer] = 5;
                        EventBus.Instance?.Publish(new CylinderLoadedEvent { ChamberIndex = _loadPointer, BulletType = _chambers[_loadPointer].Value });
                    }
                }
                else
                {
                    _chambers[_loadPointer] = Mathf.Clamp(topFace - 1, 0, 5);
                    EventBus.Instance?.Publish(new CylinderLoadedEvent { ChamberIndex = _loadPointer, BulletType = _chambers[_loadPointer].Value });
                }
            }

            PublishStateSnapshot(); // 행동 완료 후 스냅샷 전달
        }

        public bool Fire(Vector2Int origin, Vector2Int direction, out ShotFiredEvent shotEvent)
        {
            shotEvent = default;
            int? loadedBullet = _chambers[_firePointer];
            GridManager.LaserLogicResult logicResult = default;
            List<Vector3> shotPathPoints = null;

            if (direction == Vector2Int.zero)
            {
                Debug.LogError("[CylinderSystem] direction is zero in Fire");
                return false;
            }

            if (loadedBullet.HasValue)
            {
                int bulletType = loadedBullet.Value;
                if (bulletType >= 0 && bulletType < _bulletDatabase.Length && _bulletDatabase[bulletType] != null)
                {
                    if (_gridManager == null)
                    {
                        Debug.LogError("[CylinderSystem] _gridManager is null");
                        return false;
                    }

                    logicResult = _gridManager.CalculateLaserLogic(origin, direction);

                    int damageMultiplier = 1;
                    if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.RussianRoulette)
                    {
                        damageMultiplier = 10;
                    }

                    shotPathPoints = _bulletDatabase[bulletType].Execute(origin, direction, _gridManager, damageMultiplier);
                }
                else
                {
                    Debug.LogError($"[CylinderSystem] Invalid bullet type or missing SO: {bulletType}");
                }

                shotEvent = new ShotFiredEvent
                {
                    Origin = origin,
                    Direction = direction,
                    BulletType = bulletType,
                    LogicResult = logicResult,
                    PathPoints = shotPathPoints
                };
                _chambers[_firePointer] = null;

                if (DiceModeManager.Instance != null && DiceModeManager.Instance.CurrentMode == DiceMode.RussianRoulette)
                {
                    _blackjackSum = 0;
                }

                EventBus.Instance?.Publish(new CylinderFiredEvent { ChamberIndex = _firePointer, BulletType = bulletType });
            }
            else
            {
                // Dry Fire
                EventBus.Instance?.Publish(new CylinderDryFiredEvent { ChamberIndex = _firePointer });
            }

            RotateCylinder();
            PublishStateSnapshot(); // 행동 완료 후 스냅샷 전달
            return loadedBullet.HasValue;
        }

        public bool TryGetRicochetPreviewPath(Vector2Int origin, Vector2Int direction, out GridManager.LaserLogicResult logicResult)
        {
            logicResult = default;

            if (direction == Vector2Int.zero)
            {
                Debug.LogError("[CylinderSystem] direction is zero in TryGetRicochetPreviewPath");
                return false;
            }

            int? loadedType = _chambers[_firePointer];
            if (!loadedType.HasValue)
            {
                return false;
            }

            int bulletType = loadedType.Value;
            if (bulletType < 0 || bulletType >= _bulletDatabase.Length)
            {
                Debug.LogError($"[CylinderSystem] Invalid bullet type: {bulletType}");
                return false;
            }

            BulletLogicSO bulletLogic = _bulletDatabase[bulletType];
            RicochetBulletSO ricochetBullet = bulletLogic as RicochetBulletSO;
            if (ricochetBullet == null)
            {
                return false;
            }

            if (_gridManager == null)
            {
                Debug.LogError("[CylinderSystem] _gridManager is null");
                return false;
            }

            logicResult = _gridManager.CalculateLaserLogic(origin, direction);
            return logicResult.PassedTiles != null && (logicResult.PassedTiles.Count > 0 || logicResult.HitWall);
        }

        public bool TryGetCurrentBulletPreviewPath(Vector2Int origin, Vector2Int direction, out GridManager.LaserLogicResult logicResult)
        {
            logicResult = default;

            if (_gridManager == null)
            {
                Debug.LogError("[CylinderSystem] _gridManager is null");
                return false;
            }

            if (direction == Vector2Int.zero)
            {
                Debug.LogError("[CylinderSystem] direction is zero in TryGetCurrentBulletPreviewPath");
                return false;
            }

            int? loadedType = _chambers[_firePointer];
            if (!loadedType.HasValue)
            {
                return false;
            }

            int bulletType = loadedType.Value;
            if (bulletType < 0 || bulletType >= _bulletDatabase.Length)
            {
                Debug.LogError($"[CylinderSystem] Invalid bullet type: {bulletType}");
                return false;
            }

            BulletLogicSO bulletLogic = _bulletDatabase[bulletType];
            if (bulletLogic == null)
            {
                Debug.LogError($"[CylinderSystem] Missing bullet logic SO: {bulletType}");
                return false;
            }

            RicochetBulletSO ricochetBullet = bulletLogic as RicochetBulletSO;
            if (ricochetBullet != null)
            {
                logicResult = _gridManager.CalculateLaserLogic(origin, direction);
                return logicResult.PassedTiles != null && (logicResult.PassedTiles.Count > 0 || logicResult.HitWall);
            }

            logicResult = _gridManager.CalculateLaserLogic(origin, direction);
            return logicResult.PassedTiles != null && (logicResult.PassedTiles.Count > 0 || logicResult.HitWall);
        }

        public bool TryPeekAndFire(Vector2Int origin, Vector2Int direction)
        {
            int? loadedType = _chambers[_firePointer];
            if (!loadedType.HasValue)
            {
                return false;
            }

            int bulletType = loadedType.Value;
            if (bulletType < 0 || bulletType >= _bulletDatabase.Length || _bulletDatabase[bulletType] == null)
            {
                Debug.LogError($"[CylinderSystem] Invalid bullet type or missing SO: {bulletType}");
                return false;
            }

            if (_gridManager == null)
            {
                Debug.LogError("[CylinderSystem] _gridManager is null");
                return false;
            }

            List<Vector3> path = _bulletDatabase[bulletType].Execute(origin, direction, _gridManager, 1);
            GridManager.LaserLogicResult logicResult = _gridManager.CalculateLaserLogic(origin, direction);
            EventBus.Instance?.Publish(new ShotFiredEvent
            {
                Origin = origin,
                Direction = direction,
                BulletType = bulletType,
                LogicResult = logicResult,
                PathPoints = path
            });

            return true;
        }

        public void ConsumeCurrentChamberAndRotate()
        {
            int? loadedType = _chambers[_firePointer];
            _chambers[_firePointer] = null;
            EventBus.Instance?.Publish(new CylinderFiredEvent
            {
                ChamberIndex = _firePointer,
                BulletType = loadedType ?? -1
            });

            RotateCylinder();
            PublishStateSnapshot();
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
