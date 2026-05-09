using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PocDiceTactics
{
    public class GimmickEntry : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private TMP_Text _descText;

        public void Setup(BulletUIData data)
        {
            if (_iconImage == null || _nameText == null || _damageText == null || _descText == null)
            {
                Debug.LogError("UI Reference Missing!");
                return;
            }

            if (data == null)
            {
                Debug.LogError("UI Reference Missing!");
                return;
            }

            _iconImage.sprite = data.DisplayIcon;
            _nameText.text = data.DisplayName;
            _damageText.text = $"데미지: {data.DisplayDamage}";
            _descText.text = data.DisplayDescription;
            _nameText.color = data.DisplayThemeColor;
        }
    }
}
