using UnityEngine;
using TMPro;
using DG.Tweening;

public class TurnFeedbackUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _turnText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeInDuration = 0.15f;
    [SerializeField] private float _holdDuration = 0.35f;
    [SerializeField] private float _fadeOutDuration = 0.2f;
    [SerializeField] private float _startScale = 0.85f;
    [SerializeField] private float _endScale = 1.05f;

        private Sequence _sequence;

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
            }

            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }
        }

        private void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (_turnText == null || _canvasGroup == null)
            {
                Debug.LogError("[TurnFeedbackUI] Required UI references are null");
                return;
            }

            if (evt.Phase == TurnPhase.PlayerTurn)
            {
                PlayBanner("PLAYER TURN");
            }
            else if (evt.Phase == TurnPhase.EnemyTurn)
            {
                PlayBanner("ENEMY TURN");
            }
        }

        private void PlayBanner(string text)
        {
            _turnText.text = text;

            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }

            _canvasGroup.alpha = 0f;
            _turnText.rectTransform.localScale = Vector3.one * _startScale;

            _sequence = DOTween.Sequence();
            _sequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
            _sequence.Join(_turnText.rectTransform.DOScale(_endScale, _fadeInDuration));
            _sequence.AppendInterval(_holdDuration);
            _sequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
            _sequence.Join(_turnText.rectTransform.DOScale(1f, _fadeOutDuration));
        }
}
