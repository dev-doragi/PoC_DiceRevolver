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
            _ => false, // 1. 일단 벽을 무시하고 궤적 계산
            grid.IsInside
        );

        foreach (Vector2Int cell in result.PassedTiles)
        {
            pathPoints.Add(grid.CellToWorld(cell));

            // 2. 적 데미지 판별
            EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
            if (enemy != null)
            {
                enemy.TakeDamage(Damage * damageMultiplier);
            }

            // 3. 실제 순회 중 벽을 만나면 파괴 후 즉시 중단 (정합성 완벽 보장)
            if (grid.IsWall(cell))
            {
                grid.DestroyWall(cell); // 내부에 EventBus Publish 포함됨!
                break;
            }
        }

        if (result.HitWall)
        {
            pathPoints.Add(grid.CellToWorld(result.HitWallTile));
        }

        return pathPoints;
    }
}
