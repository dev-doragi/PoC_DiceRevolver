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

        Vector2 currentPos = origin;
        Vector2 dir = new Vector2(direction.x, direction.y).normalized;

        int bounceCount = 0;
        bool isPiercing = false;
        int currentDamageMultiplier = damageMultiplier;
        Vector2Int lastCell = origin;

        for (int step = 0; step < 400; step++)
        {
            currentPos += dir * _stepSize;
            Vector2Int currentCell = new Vector2Int(Mathf.RoundToInt(currentPos.x), Mathf.RoundToInt(currentPos.y));

            if (currentCell == lastCell)
            {
                continue;
            }

            if (!grid.IsInside(currentCell) || grid.IsWall(currentCell))
            {
                bounceCount++;
                if (bounceCount > _maxBounces)
                {
                    return pathPoints;
                }

                isPiercing = true;
                currentDamageMultiplier++;

                dir = ResolveBounceDirection(grid, lastCell, currentCell, dir);

                currentPos = new Vector2(lastCell.x, lastCell.y) + dir * 0.01f;
                currentCell = lastCell;
                continue;
            }

            Vector2Int delta = currentCell - lastCell;

            // 대각 이동 시 중간 셀 누락 방지 판정
            if (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1)
            {
                Vector2Int sideCellX = new Vector2Int(lastCell.x + delta.x, lastCell.y);
                Vector2Int sideCellY = new Vector2Int(lastCell.x, lastCell.y + delta.y);

                EnemyController enemyOnSideX = grid.GetOccupant(sideCellX) as EnemyController;
                if (enemyOnSideX != null)
                {
                    pathPoints.Add(grid.CellToWorld(sideCellX));
                    if (applyDamage)
                    {
                        enemyOnSideX.TakeDamage(Damage * currentDamageMultiplier);
                    }

                    if (!isPiercing)
                    {
                        return pathPoints;
                    }
                }

                EnemyController enemyOnSideY = grid.GetOccupant(sideCellY) as EnemyController;
                if (enemyOnSideY != null)
                {
                    pathPoints.Add(grid.CellToWorld(sideCellY));
                    if (applyDamage)
                    {
                        enemyOnSideY.TakeDamage(Damage * currentDamageMultiplier);
                    }

                    if (!isPiercing)
                    {
                        return pathPoints;
                    }
                }
            }

            EnemyController enemy = grid.GetOccupant(currentCell) as EnemyController;
            pathPoints.Add(grid.CellToWorld(currentCell));
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

            lastCell = currentCell;
        }

        return pathPoints;
    }

    private Vector2 ResolveBounceDirection(GridManager grid, Vector2Int lastCell, Vector2Int currentCell, Vector2 dir)
    {
        bool hitX = lastCell.x != currentCell.x;
        bool hitY = lastCell.y != currentCell.y;

        if (hitX && hitY)
        {
            if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            {
                return new Vector2(-dir.x, dir.y);
            }

            return new Vector2(dir.x, -dir.y);
        }

        if (hitX)
        {
            return new Vector2(-dir.x, dir.y);
        }

        if (hitY)
        {
            return new Vector2(dir.x, -dir.y);
        }

        if (!grid.IsInside(currentCell))
        {
            if (currentCell.x < 0 || currentCell.x >= grid.GridSize.x)
            {
                return new Vector2(-dir.x, dir.y);
            }

            if (currentCell.y < 0 || currentCell.y >= grid.GridSize.y)
            {
                return new Vector2(dir.x, -dir.y);
            }
        }

        return -dir;
    }
}
