using UnityEngine;
using UnityEngine.UI;

public class EnemyWorldUI : MonoBehaviour
{
    [SerializeField] private EnemyController _owner;
    [SerializeField] private Slider _hpSlider;

        private int _maxHp;
        private int _currentHp;

        private void Awake()
        {
            if (_owner == null)
            {
                _owner = GetComponentInParent<EnemyController>();
            }

            if (_owner == null)
            {
                Debug.LogError("[EnemyWorldUI] _owner is null");
                enabled = false;
                return;
            }

            if (_hpSlider == null)
            {
                Debug.LogError("[EnemyWorldUI] _hpSlider is null");
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }

            EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
            EventBus.Instance.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        public void Initialize(int maxHp)
        {
            _maxHp = Mathf.Max(1, maxHp);
            _currentHp = _maxHp;
            _hpSlider.minValue = 0f;
            _hpSlider.maxValue = _maxHp;
            _hpSlider.value = _currentHp;
        }

        private void OnEnemyDamaged(EnemyDamagedEvent e)
        {
            if (_owner == null)
            {
                Debug.LogError("[EnemyWorldUI] _owner is null");
                return;
            }

            if (e.EnemyPosition != _owner.GridPosition)
            {
                return;
            }

            _currentHp = Mathf.Max(0, _currentHp - e.Damage);
            _hpSlider.value = _currentHp;
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            if (_owner == null)
            {
                Debug.LogError("[EnemyWorldUI] _owner is null");
                return;
            }

            if (e.EnemyPosition != _owner.GridPosition)
            {
                return;
            }

            _hpSlider.value = 0f;
        }
}
