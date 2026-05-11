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
                Debug.LogError("[BulletLogicSO] grid is null or direction is zero");
                return null;
            }

            var result = PathCalculationService.CalculateLaserPath(
                origin, 
                new Vector2(direction.x, direction.y), 
                Mathf.Max(grid.GridSize.x, grid.GridSize.y) * 2,
                grid.IsWall,
                grid.IsInside
            );

            foreach (Vector2Int cell in result.PassedTiles)
            {
                pathPoints?.Add(grid.CellToWorld(cell));

                EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
                if (enemy != null)
                {
                    return enemy;
                }
            }

            if (result.HitWall)
            {
                pathPoints?.Add(grid.CellToWorld(result.HitWallTile));
            }

            return null;
        }
}
