using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PocDiceTactics
{
    /// <summary>
    /// 단일 탄환 UI 프리팹의 시각 정보를 갱신합니다. 아웃라인 이펙트를 활용합니다.
    /// </summary>
    public class BulletUI : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Outline _iconOutline; // 추가된 아웃라인 컴포넌트
        [SerializeField] private TextMeshProUGUI _numberText;
        [SerializeField] private TextMeshProUGUI _nameText;

        private Color _defaultIconColor = Color.white;
        private Color _defaultOutlineColor = Color.white;

        public void Setup(BulletUIData data)
        {
            if (data == null) return;

            // 1. 탄환 이미지 세팅
            if (_iconImage != null)
            {
                _iconImage.sprite = data.DisplayIcon;
                _iconImage.color = _defaultIconColor;
            }

            // 2. 아웃라인 이펙트 컬러 세팅
            if (_iconOutline != null)
            {
                Color outlineColor = data.DisplayThemeColor;
                outlineColor.a = 1f; // 외곽선 투명도 (필요 시 조절)
                _iconOutline.effectColor = outlineColor;
                _defaultOutlineColor = outlineColor;
            }

            // 3. 큰 숫자 세팅
            if (_numberText != null)
            {
                _numberText.text = data.BulletType.ToString();
                _numberText.color = Color.white;
            }

            // 4. 조그만 라벨 세팅
            if (_nameText != null)
            {
                _nameText.color = Color.white;
                _nameText.text = data.DisplayName;
            }

            _defaultIconColor = _iconImage != null ? _iconImage.color : Color.white;
        }

        public void SetMasked(Color maskColor)
        {
            if (_iconImage != null)
            {
                _iconImage.color = maskColor;
            }

            if (_iconOutline != null)
            {
                _iconOutline.effectColor = maskColor;
            }
        }

        public void SetAlphaObscured(bool obscure)
        {
            if (_iconImage != null)
            {
                _iconImage.color = obscure ? Color.black : _defaultIconColor;
            }

            if (_iconOutline != null)
            {
                _iconOutline.effectColor = obscure ? Color.black : _defaultOutlineColor;
            }

            if (_numberText != null)
            {
                _numberText.gameObject.SetActive(!obscure);
            }

            if (_nameText != null)
            {
                _nameText.gameObject.SetActive(!obscure);
            }
        }
    }
}