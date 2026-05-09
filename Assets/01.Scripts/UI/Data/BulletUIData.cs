using UnityEngine;

namespace PocDiceTactics
{
    /// <summary>
    /// 개별 탄환 UI 표시에 필요한 데이터 시트입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BulletUIData", menuName = "UI/Bullet UIData")]
    public class BulletUIData : ScriptableObject
    {
        public int BulletType;
        public BulletLogicSO BulletLogic;
        [TextArea] public string Description;

        public string DisplayName => BulletLogic != null ? BulletLogic.BulletName : string.Empty;
        public Sprite DisplayIcon => BulletLogic != null ? BulletLogic.Icon : null;
        public int DisplayDamage => BulletLogic != null ? BulletLogic.Damage : 0;
        public string DisplayDescription => !string.IsNullOrEmpty(Description) ? Description : DisplayName;
        public Color DisplayThemeColor => BulletLogic != null ? BulletLogic.ThemeColor : Color.white;
    }
}