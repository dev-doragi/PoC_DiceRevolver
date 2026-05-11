using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MagnumBullet", menuName = "PoC/Bullets/Magnum")]
public class MagnumBulletSO : BulletLogicSO
{
    public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1)
    {
        List<Vector3> pathPoints = new List<Vector3>();
        if (grid == null || direction == Vector2Int.zero)
        {
            Debug.LogError("[MagnumBulletSO] grid is null or direction is zero");
            return pathPoints;
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
            pathPoints.Add(grid.CellToWorld(cell));

            EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
            if (enemy != null)
            {
                enemy.TakeDamage(Damage * damageMultiplier);
            }
        }

        if (result.HitWall)
        {
            pathPoints.Add(grid.CellToWorld(result.HitWallTile));
            grid.DestroyWall(result.HitWallTile);
        }

        return pathPoints;
    }
}
