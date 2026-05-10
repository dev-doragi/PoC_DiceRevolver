using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RicochetBullet", menuName = "PoC/Bullets/Ricochet")]
public class RicochetBulletSO : BulletLogicSO
{
    [SerializeField] private int _maxBounces = 2;
    [SerializeField] private int _maxRangePerSegment = 10; // 세그먼트당 최대 거리

    public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1)
    {
        return SimulatePath(origin, direction, grid, true, damageMultiplier);
    }

    public List<Vector3> PredictPath(Vector2Int origin, Vector2Int direction, GridManager grid)
    {
        return SimulatePath(origin, direction, grid, false, 1);
    }

    private List<Vector3> SimulatePath(Vector2Int origin, Vector2Int direction, GridManager grid, bool applyDamage, int damageMultiplier)
    {
        List<Vector3> pathPoints = new List<Vector3>();
        if (grid == null)
        {
            Debug.LogError("[RicochetBulletSO] grid is null");
            return pathPoints;
        }

        if (direction == Vector2Int.zero)
        {
            return pathPoints;
        }

        // PathCalculationService 를 사용하여 일관된 경로 계산
        var ricochetResult = PathCalculationService.ComputeRicochetPath(
            origin, 
            direction, 
            _maxBounces, 
            _maxRangePerSegment, 
            grid as IGridDataProvider
        );

        int currentDamageMultiplier = damageMultiplier;
        
        foreach (var segment in ricochetResult.Segments)
        {
            foreach (Vector2Int cell in segment.PassedTiles)
            {
                pathPoints.Add(grid.CellToWorld(cell));

                // 적 체크 및 데미지 적용
                EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
                if (enemy != null)
                {
                    if (applyDamage)
                    {
                        enemy.TakeDamage(Damage * currentDamageMultiplier);
                    }

                    // 리코셰 탄은 관통하지 않음 (첫 번째 적에서 중지)
                    if (!segment.HitWall)
                    {
                        return pathPoints;
                    }
                }
            }

            // 벽에 닿았으면 다음 세그먼트로 계속 (데미지 멀티플라이어 증가)
            if (segment.HitWall)
            {
                currentDamageMultiplier++;
            }
        }

        return pathPoints;
    }
}
