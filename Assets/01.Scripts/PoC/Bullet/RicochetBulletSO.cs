using UnityEngine;
using System.Collections.Generic;

namespace PocDiceTactics
{
    [CreateAssetMenu(fileName = "RicochetBullet", menuName = "PoC/Bullets/Ricochet")]
    public class RicochetBulletSO : BulletLogicSO
    {
        [SerializeField] private int _maxBounces = 2;
        [SerializeField] private float _stepSize = 0.25f; // 레이캐스트 정밀도

        public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1)
        {
            List<Vector3> pathPoints = new List<Vector3>();
            if (grid == null)
            {
                Debug.LogError("[RicochetBulletSO] grid is null");
                return pathPoints;
            }

            if (direction == Vector2Int.zero) return pathPoints;

            Vector2 currentPos = origin;
            Vector2 dir = new Vector2(direction.x, direction.y).normalized;

            int bounceCount = 0;
            Vector2Int lastCell = origin;

            for (int step = 0; step < 400; step++)
            {
                currentPos += dir * _stepSize;
                Vector2Int currentCell = new Vector2Int(Mathf.RoundToInt(currentPos.x), Mathf.RoundToInt(currentPos.y));

                if (currentCell != lastCell)
                {
                    if (!grid.IsInside(currentCell) || grid.IsWall(currentCell))
                    {
                        bounceCount++;
                        if (bounceCount > _maxBounces) return pathPoints;

                        if (lastCell.x != currentCell.x && lastCell.y != currentCell.y)
                        {
                            dir = -dir;
                        }
                        else if (lastCell.x != currentCell.x)
                        {
                            dir = new Vector2(-dir.x, dir.y);
                        }
                        else if (lastCell.y != currentCell.y)
                        {
                            dir = new Vector2(dir.x, -dir.y);
                        }

                        currentPos = lastCell;
                        currentCell = lastCell;
                    }
                    else
                    {
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
                                enemyOnSideX.TakeDamage(Damage * damageMultiplier);
                                return pathPoints;
                            }

                            EnemyController enemyOnSideY = grid.GetOccupant(sideCellY) as EnemyController;
                            if (enemyOnSideY != null)
                            {
                                pathPoints.Add(grid.CellToWorld(sideCellY));
                                enemyOnSideY.TakeDamage(Damage * damageMultiplier);
                                return pathPoints;
                            }
                        }

                        EnemyController enemy = grid.GetOccupant(currentCell) as EnemyController;
                        pathPoints.Add(grid.CellToWorld(currentCell));
                        if (enemy != null)
                        {
                            enemy.TakeDamage(Damage * damageMultiplier);
                            return pathPoints;
                        }
                    }

                    lastCell = currentCell;
                }
            }

            return pathPoints;
        }
    }
}