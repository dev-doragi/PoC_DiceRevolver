using UnityEngine;
using TMPro;
using System.Collections;
using DG.Tweening;

namespace PocDiceTactics
{
    /// <summary>
    /// 단일 타일의 시각 상태(벽/과열/호버/고스트/텔레그래프)를 담당합니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TileView : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] private SpriteRenderer _baseRenderer;
        [SerializeField] private SpriteRenderer _heatRenderer;
        [SerializeField] private SpriteRenderer _telegraphRenderer;
        [SerializeField] private SpriteRenderer _hoverRenderer;
        [SerializeField] private SpriteRenderer _shotFlashRenderer;
        [SerializeField] private TextMeshPro _ghostText;
        [SerializeField] private SpriteRenderer _telegraphOutlineRenderer;
        [SerializeField] private SpriteRenderer _dangerIconRenderer;

        [Header("Colors")]
        [SerializeField] private Color _floorColor = new Color(0.11f, 0.12f, 0.15f, 1f);
        [SerializeField] private Color _wallColor = new Color(0.22f, 0.25f, 0.3f, 1f);
        [SerializeField] private Color _hoverColor = new Color(0.12f, 0.8f, 0.55f, 0.28f);
        [SerializeField] private Color _confirmColor = new Color(0.2f, 1f, 0.7f, 0.48f);
        [SerializeField] private Color _heatColor = new Color(1f, 0.22f, 0.22f, 0.35f);
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.15f, 0.15f, 0.42f);

        [Header("Animation")]
        [SerializeField] private float _heatPulseSpeed = 5f;
        [SerializeField] private float _telegraphPulseSpeed = 10f;
        [SerializeField] private float _telegraphShakeAmount = 0.03f;
        [SerializeField] private float _shotFlashDuration = 0.08f;
        [SerializeField] private float _telegraphOutlinePulseSpeed = 11f;

        private Vector3 _originLocalPosition;
        private Vector2Int _cell;
        private bool _isWall;
        private bool _isOverheated;
        private bool _isTelegraph;
        private bool _isHover;
        private bool _isConfirmHover;
        private int _predictedFace;
        private bool _isInitialized;
        private BoxCollider2D _tileCollider;
        private Coroutine _shotFlashRoutine;

        public Vector2Int Cell => _cell;

        private void Awake()
        {
            _tileCollider = GetComponent<BoxCollider2D>();
            ConfigureCollider();
            _originLocalPosition = transform.localPosition;
            ApplyImmediate();
        }

        private void OnValidate()
        {
            if (_tileCollider == null)
            {
                _tileCollider = GetComponent<BoxCollider2D>();
            }

            ConfigureCollider();
        }

        private void Update()
        {
            AnimateOverheat();
            AnimateTelegraph();
            UpdateGhostText();
        }

        private void OnEnable()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (GridManager.Instance == null)
            {
                Debug.LogError("[TileView] GridManager.Instance is null");
                return;
            }

            SetOverheated(GridManager.Instance.IsOverheated(_cell));
        }

        public void Initialize(Vector2Int cell, bool isWall)
        {
            _cell = cell;
            _isWall = isWall;
            _isOverheated = false;
            _isTelegraph = false;
            _isHover = false;
            _isConfirmHover = false;
            _predictedFace = 0;
            _originLocalPosition = transform.localPosition;
            _isInitialized = true;
            ConfigureCollider();
            ApplyImmediate();

            if (GridManager.Instance != null)
            {
                SetOverheated(GridManager.Instance.IsOverheated(_cell));
            }
        }

        public void SetWall(bool isWall)
        {
            _isWall = isWall;
            ApplyImmediate();
        }

        public void SetOverheated(bool isOverheated)
        {
            _isOverheated = isOverheated;
            if (_heatRenderer != null)
            {
                _heatRenderer.enabled = _isOverheated;
                Color color = _heatColor;
                _heatRenderer.color = color;
            }
        }

        public void SetTelegraph(bool isActive)
        {
            _isTelegraph = isActive;
            if (_telegraphRenderer != null)
            {
                _telegraphRenderer.enabled = _isTelegraph;
                _telegraphRenderer.color = _telegraphColor;
            }

            if (_telegraphOutlineRenderer != null)
            {
                _telegraphOutlineRenderer.enabled = _isTelegraph;
            }

            if (_dangerIconRenderer != null)
            {
                _dangerIconRenderer.enabled = _isTelegraph;
            }

            if (!_isTelegraph)
            {
                transform.localPosition = _originLocalPosition;
            }
        }

        public void SetHover(bool isHover, int predictedFace, bool isConfirmRequired)
        {
            _isHover = isHover;
            _predictedFace = predictedFace;
            _isConfirmHover = isConfirmRequired;

            if (_hoverRenderer != null)
            {
                _hoverRenderer.enabled = _isHover;
                _hoverRenderer.color = _isConfirmHover ? _confirmColor : _hoverColor;
            }
        }

        public void ClearTransient()
        {
            SetHover(false, 0, false);
            SetTelegraph(false);
            if (_shotFlashRenderer != null)
            {
                _shotFlashRenderer.enabled = false;
            }
        }

        public void PlayShotFlash(Color color)
        {
            if (_shotFlashRenderer == null)
            {
                return;
            }

            if (_shotFlashRoutine != null)
            {
                StopCoroutine(_shotFlashRoutine);
            }

            _shotFlashRoutine = StartCoroutine(ShotFlashRoutine(color));
        }

        private void ApplyImmediate()
        {
            if (_baseRenderer != null)
            {
                _baseRenderer.color = _isWall ? _wallColor : _floorColor;
            }

            if (_heatRenderer != null)
            {
                _heatRenderer.enabled = _isOverheated;
                _heatRenderer.color = _heatColor;
            }

            if (_telegraphRenderer != null)
            {
                _telegraphRenderer.enabled = _isTelegraph;
                _telegraphRenderer.color = _telegraphColor;
            }

            if (_telegraphOutlineRenderer != null)
            {
                _telegraphOutlineRenderer.enabled = _isTelegraph;
                _telegraphOutlineRenderer.color = _telegraphColor;
            }

            if (_dangerIconRenderer != null)
            {
                _dangerIconRenderer.enabled = _isTelegraph;
            }

            if (_hoverRenderer != null)
            {
                _hoverRenderer.enabled = _isHover;
                _hoverRenderer.color = _isConfirmHover ? _confirmColor : _hoverColor;
            }

            if (_shotFlashRenderer != null)
            {
                _shotFlashRenderer.enabled = false;
            }

            UpdateGhostText();
        }

        private void UpdateGhostText()
        {
            if (_ghostText == null) return;

            bool show = _isHover && !_isWall && _predictedFace > 0;
            _ghostText.gameObject.SetActive(show);

            if (show)
            {
                _ghostText.text = _predictedFace.ToString();
                _ghostText.color = _isConfirmHover
                    ? new Color(0.45f, 1f, 0.75f, 1f)
                    : new Color(0.45f, 1f, 0.75f, 0.65f);
            }
        }

        private void AnimateOverheat()
        {
            if (!_isOverheated || _heatRenderer == null) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _heatPulseSpeed);
            Color color = _heatColor;
            color.a = Mathf.Lerp(0.22f, 0.55f, pulse);
            _heatRenderer.color = color;
        }

        private void AnimateTelegraph()
        {
            if (!_isTelegraph || _telegraphRenderer == null) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _telegraphPulseSpeed);
            Color color = _telegraphColor;
            color.a = Mathf.Lerp(0.2f, 0.65f, pulse);
            _telegraphRenderer.color = color;

            float shakeX = Mathf.Sin(Time.time * 34f) * _telegraphShakeAmount;
            float shakeY = Mathf.Cos(Time.time * 29f) * _telegraphShakeAmount;
            transform.localPosition = _originLocalPosition + new Vector3(shakeX, shakeY, 0f);

            if (_telegraphOutlineRenderer != null)
            {
                float outlinePulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _telegraphOutlinePulseSpeed);
                Color outlineColor = _telegraphColor;
                outlineColor.a = Mathf.Lerp(0.35f, 0.95f, outlinePulse);
                _telegraphOutlineRenderer.color = outlineColor;
            }

            if (_dangerIconRenderer != null)
            {
                float iconPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (_telegraphPulseSpeed * 0.7f));
                Color iconColor = _dangerIconRenderer.color;
                iconColor.a = Mathf.Lerp(0.45f, 1f, iconPulse);
                _dangerIconRenderer.color = iconColor;
            }
        }

        private void ConfigureCollider()
        {
            if (_tileCollider == null || _baseRenderer == null)
            {
                return;
            }

            _tileCollider.isTrigger = true;

            Bounds bounds = _baseRenderer.bounds;
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);

            _tileCollider.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
            _tileCollider.offset = new Vector2(localCenter.x, localCenter.y);
        }

        private IEnumerator ShotFlashRoutine(Color color)
        {
            _shotFlashRenderer.enabled = true;
            _shotFlashRenderer.color = color;
            yield return new WaitForSeconds(_shotFlashDuration);
            _shotFlashRenderer.enabled = false;
            _shotFlashRoutine = null;
        }
    }
}