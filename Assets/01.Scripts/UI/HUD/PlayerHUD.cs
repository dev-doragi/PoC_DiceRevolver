using UnityEngine;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _apText;
    [SerializeField] private TextMeshProUGUI _hpText;

        private void OnEnable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }

            EventBus.Instance.Subscribe<PlayerAPChangedEvent>(OnAPChanged);
            EventBus.Instance.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Instance.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void Start()
        {
            if (_hpText != null)
            {
                if (PlayerManager.Instance != null && PlayerManager.Instance.Status != null)
                {
                    UpdateHPText(PlayerManager.Instance.Status.CurrentHP);
                }
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }

            EventBus.Instance.Unsubscribe<PlayerAPChangedEvent>(OnAPChanged);
            EventBus.Instance.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            EventBus.Instance.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnAPChanged(PlayerAPChangedEvent evt)
        {
            if (_apText == null)
            {
                Debug.LogError("[PlayerHUD] _apText is null");
                return;
            }

            _apText.text = $"AP: {evt.CurrentAP} ★";
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (_apText == null)
            {
                Debug.LogError("[PlayerHUD] _apText is null");
                return;
            }

            if (_hpText == null)
            {
                Debug.LogError("[PlayerHUD] _hpText is null");
                return;
            }

            if (evt.Phase == TurnPhase.RoundTransition || evt.Phase == TurnPhase.GameOver)
            {
                _apText.gameObject.SetActive(false);
                _hpText.gameObject.SetActive(false);
                return;
            }

            if (evt.Phase == TurnPhase.PlayerTurn || evt.Phase == TurnPhase.EnemyTurn)
            {
                _apText.gameObject.SetActive(true);
                _hpText.gameObject.SetActive(true);
            }
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            UpdateHPText(evt.RemainingHp);
        }

        private void UpdateHPText(int remainingHp)
        {
            if (_hpText == null)
            {
                Debug.LogError("[PlayerHUD] _hpText is null");
                return;
            }

            _hpText.text = $"HP: {new string('♥', Mathf.Max(0, remainingHp))}";
        }
}
