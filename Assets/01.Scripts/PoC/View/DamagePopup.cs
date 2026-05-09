using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _damageText;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private float _riseDistance = 48f;
    [SerializeField] private float _duration = 0.45f;

        private Vector2 _originAnchoredPosition;

        private void Awake()
        {
            if (_damageText == null)
            {
                _damageText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            if (_damageText == null)
            {
                Debug.LogError("[DamagePopup] _damageText is null");
                enabled = false;
                return;
            }

            if (_canvasGroup == null)
            {
                Debug.LogError("[DamagePopup] _canvasGroup is null");
                enabled = false;
                return;
            }

            if (_rectTransform == null)
            {
                Debug.LogError("[DamagePopup] _rectTransform is null");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            // 1. 이 오브젝트에 붙어있는 Canvas 컴포넌트를 가져옵니다.
            Canvas canvas = GetComponent<Canvas>();

            // 2. 씬에 있는 메인 카메라를 찾아서 Event Camera에 할당합니다.
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
        }

        public void Play(int damage)
        {
            if (_damageText == null || _canvasGroup == null || _rectTransform == null)
            {
                Debug.LogError("[DamagePopup] popup references are invalid");
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError("[DamagePopup] PoolManager.Instance is null");
                return;
            }

            DOTween.Kill(this);

            _damageText.text = damage.ToString();
            _canvasGroup.alpha = 1f;
            _originAnchoredPosition = _rectTransform.anchoredPosition;
            _rectTransform.anchoredPosition = _originAnchoredPosition;

            Sequence sequence = DOTween.Sequence().SetId(this);
            sequence.Join(_rectTransform.DOAnchorPosY(_originAnchoredPosition.y + _riseDistance, _duration).SetEase(Ease.OutQuad));
            sequence.Join(_canvasGroup.DOFade(0f, _duration).SetEase(Ease.Linear));
            sequence.OnComplete(() =>
            {
                if (PoolManager.Instance == null)
                {
                    Debug.LogError("[DamagePopup] PoolManager.Instance is null");
                    return;
                }

                PoolManager.Instance.Despawn(gameObject);
            });
        }
}
