using UnityEngine;
using System.Collections.Generic;

public abstract class BulletLogicSO : ScriptableObject
{
    public string BulletName;
    public int Damage;
    public Sprite Icon;
    public Color ThemeColor = Color.white;

        public abstract List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1);

        protected EnemyController GetFirstEnemyInLine(Vector2Int origin, Vector2Int direction, GridManager grid, List<Vector3> pathPoints)
        {
            if (grid == null || direction == Vector2Int.zero)
            {
                return null;
            }

            foreach (Vector2Int cell in grid.GetBresenhamLine(origin, direction))
            {
                if (!grid.IsInside(cell))
                {
                    return null;
                }

                pathPoints?.Add(grid.CellToWorld(cell));

                if (grid.IsWall(cell))
                {
                    return null;
                }

                EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
                if (enemy != null)
                {
                    return enemy;
                }
            }

            return null;
        }
}
