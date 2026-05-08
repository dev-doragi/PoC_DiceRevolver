using UnityEngine;
using System.Collections.Generic;

namespace PocDiceTactics
{
    [CreateAssetMenu(fileName = "RicochetBullet", menuName = "PoC/Bullets/Ricochet")]
    public class RicochetBulletSO : BulletLogicSO
    {
        [SerializeField] private int _maxBounces = 2;
        [SerializeField] private float _stepSize = 0.25f; // 레이캐스트 정밀도

        public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid)
        {
            List<Vector3> pathPoints = new List<Vector3>();
            if (grid == null || direction == Vector2Int.zero) return pathPoints;

            // 1. 당구 물리 연산을 위해 정수가 아닌 실수(Float) 방향 벡터로 변환 (정규화)
            Vector2 currentPos = origin;
            Vector2 dir = new Vector2(direction.x, direction.y).normalized;

            int bounceCount = 0;
            Vector2Int lastCell = origin;

            // 무한 루프 방지 (최대 100타일 사거리)
            for (int step = 0; step < 400; step++)
            {
                // 2. 총알을 지정된 방향으로 조금씩 전진시킴 (Raymarching)
                currentPos += dir * _stepSize;
                Vector2Int currentCell = new Vector2Int(Mathf.RoundToInt(currentPos.x), Mathf.RoundToInt(currentPos.y));

                // 3. 총알이 새로운 타일로 넘어갔을 때만 판정
                if (currentCell != lastCell)
                {
                    // 벽이거나 맵 경계선 밖이라면 튕겨낸다!
                    if (!grid.IsInside(currentCell) || grid.IsWall(currentCell))
                    {
                        bounceCount++;
                        if (bounceCount > _maxBounces) return pathPoints; // 튕김 횟수 초과 시 소멸

                        // 입사각/반사각 판별: 어느 축의 면에 부딪혔는가?
                        if (lastCell.x != currentCell.x && lastCell.y != currentCell.y)
                        {
                            // 완벽한 대각선 모서리에 꽂힘 -> 완전히 정반대로 튕김
                            dir = -dir;
                        }
                        else if (lastCell.x != currentCell.x)
                        {
                            // 좌우(세로) 벽에 맞음 -> X축 이동 방향만 반전
                            dir = new Vector2(-dir.x, dir.y);
                        }
                        else if (lastCell.y != currentCell.y)
                        {
                            // 상하(가로) 벽에 맞음 -> Y축 이동 방향만 반전
                            dir = new Vector2(dir.x, -dir.y);
                        }

                        // 튕겨나갔으므로, 시각적 오류를 막기 위해 위치를 튕기기 전 타일로 되돌림
                        currentPos = lastCell;
                        currentCell = lastCell;
                    }
                    else
                    {
                        // 벽이 아니라면 적이 있는지 확인
                        EnemyController enemy = grid.GetOccupant(currentCell) as EnemyController;
                        pathPoints.Add(grid.CellToWorld(currentCell));
                        if (enemy != null)
                        {
                            enemy.TakeDamage(Damage);
                            return pathPoints; // 타격 후 소멸
                        }
                    }

                    lastCell = currentCell;
                }
            }

            return pathPoints;
        }
    }
}