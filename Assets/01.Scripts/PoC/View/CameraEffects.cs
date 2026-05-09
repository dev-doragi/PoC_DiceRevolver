using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CameraEffects : MonoBehaviour
{
    [SerializeField] private Image _flashOverlay;
    [SerializeField] private float _shotShakeDuration = 0.12f;
    [SerializeField] private float _shotShakeStrength = 0.15f;
    [SerializeField] private int _shotShakeVibrato = 20;
    [SerializeField] private float _damageShakeDuration = 0.18f;
    [SerializeField] private float _damageShakeStrength = 0.22f;
    [SerializeField] private int _damageShakeVibrato = 28;
    [SerializeField] private float _flashDuration = 0.16f;
    [SerializeField] private float _flashAlpha = 0.25f;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[CameraEffects] Camera.main is null");
                enabled = false;
                return;
            }

            if (_flashOverlay == null)
            {
                Debug.LogError("[CameraEffects] _flashOverlay is null");
                enabled = false;
                return;
            }

            Color color = _flashOverlay.color;
            color.a = 0f;
            _flashOverlay.color = color;
        }

        private void OnEnable()
        {
            EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFired);
            EventBus.Instance.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnDisable()
        {
            if (EventBus.Instance == null)
            {
                return;
            }

            EventBus.Instance.Unsubscribe<ShotFiredEvent>(OnShotFired);
            EventBus.Instance.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        }

        private void OnShotFired(ShotFiredEvent _)
        {
            if (_mainCamera == null)
            {
                Debug.LogError("[CameraEffects] _mainCamera is null");
                return;
            }

            _mainCamera.transform.DOComplete();
            _mainCamera.transform.DOShakePosition(_shotShakeDuration, _shotShakeStrength, _shotShakeVibrato);
            PlayFlash();
        }

        private void OnPlayerDamaged(PlayerDamagedEvent _)
        {
            if (_mainCamera == null)
            {
                Debug.LogError("[CameraEffects] _mainCamera is null");
                return;
            }

            _mainCamera.transform.DOComplete();
            _mainCamera.transform.DOShakePosition(_damageShakeDuration, _damageShakeStrength, _damageShakeVibrato);
            PlayFlash();
        }

        private void PlayFlash()
        {
            if (_flashOverlay == null)
            {
                Debug.LogError("[CameraEffects] _flashOverlay is null");
                return;
            }

            _flashOverlay.DOKill();
            Color baseColor = _flashOverlay.color;
            baseColor.a = _flashAlpha;
            _flashOverlay.color = baseColor;
            _flashOverlay.DOFade(0f, _flashDuration).SetEase(Ease.OutQuad);
        }
}
