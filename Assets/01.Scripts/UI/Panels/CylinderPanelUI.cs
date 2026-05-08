using UnityEngine;
using DG.Tweening; // DOTween 필수

namespace PocDiceTactics
{
    /// <summary>
    /// 실린더 슬롯 전체의 UI 상태를 관리하고, 휠을 물리적으로 회전시킵니다.
    /// </summary>
    public class CylinderPanelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _cylinderWheel; // 6개 슬롯을 담고 있는 회전축
        [SerializeField] private RectTransform[] _slotAnchors = new RectTransform[6];
        [SerializeField] private BulletUIData[] _bulletDataConfigs = new BulletUIData[6];
        [SerializeField] private GameObject _bulletUIPrefab;
        [SerializeField] private string _bulletPrefabKey = "BulletUI_Default";

        [Header("Rotation Settings")]
        [SerializeField] private float _rotationDuration = 0.3f;

        private readonly BulletUI[] _activeBulletUIs = new BulletUI[6];
        private bool _isPoolInitialized;
        private int _lastFirePointer = -1; // 이전 포인터 위치 추적용

        private void Start()
        {
            EnsurePoolReady();
        }

        private void OnEnable()
        {
            EventBus.Instance?.Subscribe<CylinderStateChangedEvent>(RefreshUI);
        }

        private void OnDisable()
        {
            EventBus.Instance?.Unsubscribe<CylinderStateChangedEvent>(RefreshUI);
            _cylinderWheel?.DOKill();
        }

        private void RefreshUI(CylinderStateChangedEvent evt)
        {
            if (!EnsurePoolReady()) return;
            bool isRouletteMode = GameModeManager.Instance != null && GameModeManager.Instance.CurrentMode == DiceMode.RussianRoulette;

            // 1. 탄환 UI 갱신 (기존과 동일)
            for (int i = 0; i < 6; i++)
            {
                if (_activeBulletUIs[i] != null)
                {
                    PoolManager.Instance.Despawn(_activeBulletUIs[i].gameObject);
                    _activeBulletUIs[i] = null;
                }

                int? bulletType = evt.Chambers[i];
                if (bulletType.HasValue || isRouletteMode)
                {
                    GameObject bulletObj = PoolManager.Instance.Spawn(_bulletPrefabKey, Vector3.zero, Quaternion.identity);
                    if (bulletObj == null) continue;

                    bulletObj.transform.SetParent(_slotAnchors[i], false);
                    bulletObj.transform.localPosition = Vector3.zero;
                    bulletObj.transform.localScale = Vector3.one;

                    BulletUI bulletUI = bulletObj.GetComponent<BulletUI>();
                    if (bulletUI != null)
                    {
                        if (isRouletteMode)
                        {
                            BulletUIData fallbackData = bulletType.HasValue ? GetBulletData(bulletType.Value) : GetFallbackBulletData();
                            bulletUI.Setup(fallbackData);
                        }
                        else
                        {
                            bulletUI.Setup(GetBulletData(bulletType.Value));
                        }

                        bulletUI.SetAlphaObscured(isRouletteMode);
                        _activeBulletUIs[i] = bulletUI;
                    }
                }
            }

            // 2. 휠 물리적 회전 처리 (새로 추가됨)
            RotateWheelToFirePointer(evt.FirePointer);
        }

        private void RotateWheelToFirePointer(int currentFirePointer)
        {
            if (_cylinderWheel == null)
            {
                Debug.LogError("[CylinderPanelUI] _cylinderWheel is not assigned.");
                return;
            }

            // 최초 갱신 시에는 애니메이션 없이 즉시 각도 적용
            if (_lastFirePointer == -1)
            {
                float initialAngle = currentFirePointer * -60f;
                _cylinderWheel.localEulerAngles = new Vector3(0, 0, initialAngle);
                ApplyChildCounterRotation();
                _lastFirePointer = currentFirePointer;
                return;
            }

            if (_lastFirePointer == currentFirePointer) return;

            _cylinderWheel.DOKill(true);
            _cylinderWheel.DOLocalRotate(new Vector3(0f, 0f, -60f), _rotationDuration, RotateMode.LocalAxisAdd)
                .OnUpdate(ApplyChildCounterRotation)
                .OnComplete(ApplyChildCounterRotation)
                .SetEase(Ease.OutBack);

            _lastFirePointer = currentFirePointer;
        }

        private BulletUIData GetBulletData(int type)
        {
            if (type < 0 || type > 5)
            {
                return null;
            }

            foreach (var data in _bulletDataConfigs)
            {
                if (data != null && data.BulletType == type) return data;
            }
            return null;
        }

        private BulletUIData GetFallbackBulletData()
        {
            if (_bulletDataConfigs == null)
            {
                return null;
            }

            for (int i = 0; i < _bulletDataConfigs.Length; i++)
            {
                if (_bulletDataConfigs[i] != null)
                {
                    return _bulletDataConfigs[i];
                }
            }

            return null;
        }

        private bool EnsurePoolReady()
        {
            // ... (기존과 동일) ...
            if (PoolManager.Instance == null || _bulletDataConfigs == null || _bulletDataConfigs.Length == 0 || string.IsNullOrEmpty(_bulletPrefabKey)) return false;
            if (_isPoolInitialized) return true;
            if (_bulletUIPrefab == null) return false;

            _bulletUIPrefab.name = _bulletPrefabKey;
            PoolManager.Instance.CreatePool(_bulletUIPrefab, 6, 24);
            _isPoolInitialized = true;
            return true;
        }

        private void ApplyChildCounterRotation()
        {
            if (_cylinderWheel == null)
            {
                return;
            }

            float inverseZ = -_cylinderWheel.localEulerAngles.z;
            for (int i = 0; i < _activeBulletUIs.Length; i++)
            {
                if (_activeBulletUIs[i] == null)
                {
                    continue;
                }

                RectTransform rect = _activeBulletUIs[i].transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                rect.localEulerAngles = new Vector3(0f, 0f, inverseZ);
            }
        }
    }
}