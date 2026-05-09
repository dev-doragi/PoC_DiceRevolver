using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PierceBullet", menuName = "PoC/Bullets/Pierce")]
public class PierceBulletSO : BulletLogicSO
{
    public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1)
        {
            List<Vector3> pathPoints = new List<Vector3>();
            if (grid == null || direction == Vector2Int.zero)
            {
                return pathPoints;
            }

            foreach (Vector2Int cell in grid.GetBresenhamLine(origin, direction))
            {
                if (!grid.IsInside(cell))
                {
                    break;
                }

                pathPoints.Add(grid.CellToWorld(cell));

                EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
                if (enemy != null)
                {
                    enemy.TakeDamage(Damage * damageMultiplier);
                }
            }

            return pathPoints;
        }
}
