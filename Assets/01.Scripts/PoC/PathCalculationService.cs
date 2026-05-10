using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 그리드 기반 총알 경로 계산 전용 서비스입니다.
/// GridManager 의 책임을 줄이고, 모든 Bullet 타입이 일관된 경로 계산을 사용하도록 합니다.
/// </summary>
public static class PathCalculationService
{
    public struct LaserLogicResult
    {
        public List<Vector2Int> PassedTiles;
        public Vector2Int HitWallTile;
        public bool HitWall;
    }

    public struct RicochetSegment
    {
        public List<Vector2Int> PassedTiles;
        public Vector2Int BouncePoint;
        public Vector2Int NewDirection;
        public bool HitWall;
    }

    public struct RicochetLogicResult
    {
        public List<RicochetSegment> Segments;
        public List<Vector2Int> AllPassedTiles;
    }

    /// <summary>
    /// 직선 레이저 경로를 계산합니다 (DDA 알고리즘).
    /// </summary>
    public static LaserLogicResult ComputeStraightPath(Vector2Int startTile, Vector2Int direction, int maxRange, IGridDataProvider grid)
    {
        if (direction == Vector2Int.zero)
        {
            Debug.LogError("[PathCalculationService] direction is zero in ComputeStraightPath");
            return default;
        }

        LaserLogicResult result = new LaserLogicResult
        {
            PassedTiles = new List<Vector2Int>(),
            HitWallTile = Vector2Int.zero,
            HitWall = false
        };

        int safeMaxRange = Mathf.Max(1, maxRange);
        Vector2 normalized = new Vector2(direction.x, direction.y).normalized;

        int stepX = normalized.x > 0f ? 1 : (normalized.x < 0f ? -1 : 0);
        int stepY = normalized.y > 0f ? 1 : (normalized.y < 0f ? -1 : 0);

        float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / normalized.x);
        float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / normalized.y);

        float tMaxX = stepX == 0 ? float.PositiveInfinity : 0.5f / Mathf.Abs(normalized.x);
        float tMaxY = stepY == 0 ? float.PositiveInfinity : 0.5f / Mathf.Abs(normalized.y);

        int x = startTile.x;
        int y = startTile.y;

        for (int i = 0; i < safeMaxRange; i++)
        {
            if (tMaxX < tMaxY)
            {
                x += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                y += stepY;
                tMaxY += tDeltaY;
            }

            Vector2Int tile = new Vector2Int(x, y);
            if (!grid.IsInside(tile))
            {
                break;
            }

            if (grid.IsWall(tile))
            {
                result.HitWall = true;
                result.HitWallTile = tile;
                return result;
            }

            result.PassedTiles.Add(tile);
        }

        return result;
    }

    /// <summary>
    /// 리코셰 탄 경로를 계산합니다 (최대 bounces 만큼 벽에서 튕김).
    /// </summary>
    public static RicochetLogicResult ComputeRicochetPath(Vector2Int origin, Vector2Int direction, int maxBounces, int maxRange, IGridDataProvider grid)
    {
        RicochetLogicResult result = new RicochetLogicResult
        {
            Segments = new List<RicochetSegment>(),
            AllPassedTiles = new List<Vector2Int>()
        };

        if (direction == Vector2Int.zero || grid == null)
        {
            return result;
        }

        Vector2Int currentDir = direction;
        Vector2Int currentPos = origin;
        int bounceCount = 0;

        while (bounceCount <= maxBounces)
        {
            LaserLogicResult segment = ComputeStraightPath(currentPos, currentDir, maxRange, grid);

            RicochetSegment ricochetSegment = new RicochetSegment
            {
                PassedTiles = segment.PassedTiles,
                BouncePoint = segment.HitWallTile,
                NewDirection = currentDir,
                HitWall = segment.HitWall
            };

            result.Segments.Add(ricochetSegment);
            result.AllPassedTiles.AddRange(segment.PassedTiles);

            if (!segment.HitWall)
            {
                // 벽에 안 닿으면 경로 종료
                break;
            }

            // 벽에 닿았으면 반사 방향 계산
            currentDir = ReflectDirection(currentDir, segment.HitWallTile, currentPos, grid);
            currentPos = segment.HitWallTile;
            bounceCount++;

            // 같은 타일에서 무한 루프 방지
            if (bounceCount > maxBounces)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// 벽 충돌 시 반사 방향을 계산합니다.
    /// </summary>
    private static Vector2Int ReflectDirection(Vector2Int incomingDir, Vector2Int hitTile, Vector2Int fromTile, IGridDataProvider grid)
    {
        Vector2Int delta = hitTile - fromTile;
        bool hitX = delta.x != 0;
        bool hitY = delta.y != 0;

        // 대각선 이동 중 벽 충돌
        if (hitX && hitY)
        {
            // 더 큰 성분 기준으로 반사
            if (Mathf.Abs(incomingDir.x) >= Mathf.Abs(incomingDir.y))
            {
                return new Vector2Int(-incomingDir.x, incomingDir.y);
            }
            return new Vector2Int(incomingDir.x, -incomingDir.y);
        }

        // X 축 방향으로 충돌
        if (hitX)
        {
            return new Vector2Int(-incomingDir.x, incomingDir.y);
        }

        // Y 축 방향으로 충돌
        if (hitY)
        {
            return new Vector2Int(incomingDir.x, -incomingDir.y);
        }

        // 그 외에는 반대 방향으로
        return new Vector2Int(-incomingDir.x, -incomingDir.y);
    }

    /// <summary>
    /// Bresenham 라인 알고리즘으로 셀 열거를 제공합니다.
    /// </summary>
    public static IEnumerable<Vector2Int> GetBresenhamLine(Vector2Int origin, Vector2Int delta, int maxDistance = 100)
    {
        if (delta == Vector2Int.zero)
        {
            yield break;
        }

        int x0 = origin.x;
        int y0 = origin.y;
        int x1 = x0 + delta.x;
        int y1 = y0 + delta.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            int e2 = err * 2;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }

            Vector2Int next = new Vector2Int(x0, y0);
            if (maxDistance > 0 && Mathf.Abs(next.x - origin.x) + Mathf.Abs(next.y - origin.y) > maxDistance)
            {
                yield break;
            }

            yield return next;
        }
    }
}

/// <summary>
/// 그리드 데이터 접근을 위한 인터페이스 (GridManager 의존성 제거용)
/// </summary>
public interface IGridDataProvider
{
    bool IsInside(Vector2Int cell);
    bool IsWall(Vector2Int cell);
    MonoBehaviour GetOccupant(Vector2Int cell);
    Vector2Int GridSize { get; }
}
