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
        public string BulletName;
        public Sprite BulletIcon;
        public Color ThemeColor;
    }
}