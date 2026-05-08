using UnityEngine;
using System.Collections.Generic;

namespace PocDiceTactics
{
    [CreateAssetMenu(fileName = "KnockbackBullet", menuName = "PoC/Bullets/Knockback")]
    public class KnockbackBulletSO : BulletLogicSO
    {
        [SerializeField] private int _wallCollisionDamage = 1;

        public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1)
        {
            List<Vector3> pathPoints = new List<Vector3>();
            if (grid == null || direction == Vector2Int.zero)
            {
                return pathPoints;
            }

            EnemyController enemy = GetFirstEnemyInLine(origin, direction, grid, pathPoints);
            if (enemy == null)
            {
                return pathPoints;
            }

            enemy.TakeDamage(Damage * damageMultiplier);

            Vector2Int pushStep = new Vector2Int(
                direction.x == 0 ? 0 : (direction.x > 0 ? 1 : -1),
                direction.y == 0 ? 0 : (direction.y > 0 ? 1 : -1));

            Vector2Int targetCell = enemy.GridPosition + pushStep;
            if (!grid.IsInside(targetCell) || grid.IsWall(targetCell))
            {
                enemy.TakeDamage(_wallCollisionDamage);
                return pathPoints;
            }

            bool pushed = enemy.TryPush(pushStep);
            if (!pushed)
            {
                enemy.TakeDamage(_wallCollisionDamage);
            }

            return pathPoints;
        }
    }
}
