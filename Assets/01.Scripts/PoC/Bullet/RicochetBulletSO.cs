using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "RicochetBullet", menuName = "PoC/Bullets/Ricochet")]
public class RicochetBulletSO : BulletLogicSO
{
    [SerializeField] private int _maxBounces = 2;
    [SerializeField] private float _stepSize = 0.25f; // 유지 중인 인스펙터 값

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

        PathCalculationService.RicochetPathResult result = PathCalculationService.CalculateRicochetPath(
            origin,
            direction,
            _maxBounces,
            Mathf.Max(grid.GridSize.x, grid.GridSize.y) * 2,
            grid.IsInside,
            grid.IsWall);

        foreach (Vector2Int cell in result.PassedTiles)
        {
            pathPoints.Add(grid.CellToWorld(cell));

            EnemyController enemy = grid.GetOccupant(cell) as EnemyController;
            if (enemy != null && applyDamage)
            {
                enemy.TakeDamage(Damage * damageMultiplier);
            }
        }

        return pathPoints;
    }
}
