using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RicochetBullet", menuName = "PoC/Bullets/Ricochet")]
public class RicochetBulletSO : BulletLogicSO
{
    [SerializeField] private int _maxBounces = 2;
    [SerializeField] private float _stepSize = 0.25f; // 레이캐스트 정밀도

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

        // PathCalculationService 를 사용하여 경로 계산 (GridManager 책임 분리)
        var ricochetResult = PathCalculationService.CalculateRicochetPath(origin, direction, _maxBounces, _stepSize, grid);
        
        Vector2 dir = new Vector2(direction.x, direction.y).normalized;
        Vector2 currentPos = origin;
        Vector2Int lastCell = origin;
        int bounceCount = 0;
        bool isPiercing = false;
        int currentDamageMultiplier = damageMultiplier;

        // 계산된 경로를 따라 타일 순회하며 적 처리
        foreach (var tile in ricochetResult.AllPassedTiles)
        {
            pathPoints.Add(grid.CellToWorld(tile));
            
            EnemyController enemy = grid.GetOccupant(tile) as EnemyController;
            if (enemy != null)
            {
                if (applyDamage)
                {
                    enemy.TakeDamage(Damage * currentDamageMultiplier);
                }

                if (!isPiercing)
                {
                    return pathPoints;
                }
            }
        }

        return pathPoints;
    }
}
