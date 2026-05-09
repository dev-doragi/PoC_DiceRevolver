using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GimmickEntry : MonoBehaviour
{
    [SerializeField] private BulletUI _bulletUI;
    [SerializeField] private TMP_Text _damageText;
    [SerializeField] private TMP_Text _descText;

        public void Setup(BulletUIData data)
        {
            if (_bulletUI == null || _damageText == null || _descText == null)
            {
                Debug.LogError("UI Reference Missing!");
                return;
            }

            if (data == null)
            {
                Debug.LogError("UI Reference Missing!");
                return;
            }

            _bulletUI.Setup(data);
            _damageText.text = $"데미지: {data.DisplayDamage}";
            _descText.text = data.DisplayDescription;
        }
}
