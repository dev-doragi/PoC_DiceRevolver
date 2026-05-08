using UnityEngine;
using System.Collections.Generic;

namespace PocDiceTactics
{
    [CreateAssetMenu(fileName = "NormalBullet", menuName = "PoC/Bullets/Normal")]
    public class NormalBulletSO : BulletLogicSO
    {
        public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid)
        {
            List<Vector3> pathPoints = new List<Vector3>();
            EnemyController enemy = GetFirstEnemyInLine(origin, direction, grid, pathPoints);
            if (enemy != null)
            {
                enemy.TakeDamage(Damage);
            }

            return pathPoints;
        }
    }
}
