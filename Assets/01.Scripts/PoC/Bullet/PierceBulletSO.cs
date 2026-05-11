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
            Debug.LogError("[PierceBulletSO] grid is null or direction is zero");
            return pathPoints;
        }

        var result = PathCalculationService.CalculateLaserPath(
            origin,
            new Vector2(direction.x, direction.y),
            Mathf.Max(grid.GridSize.x, grid.GridSize.y) * 2,
            _ => false, // 벽을 완전 무시
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
            // Pierce는 IsWall 검사 및 break 로직이 아예 없음! (완전 관통)
        }

        if (result.HitWall)
        {
            pathPoints.Add(grid.CellToWorld(result.HitWallTile));
        }

        return pathPoints;
    }
}
