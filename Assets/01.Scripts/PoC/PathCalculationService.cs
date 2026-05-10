using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 총알 경로 계산 (Laser, Ricochet 등) 을 담당하는 순수 계산 서비스입니다.
/// GridManager 의 책임을 줄이고 Preview/Actual Path 일치를 보장합니다.
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
        public Vector2Int HitWallTile;
        public bool HitWall;
        public Vector2Int Direction;
    }

    public struct RicochetLogicResult
    {
        public List<RicochetSegment> Segments;
        public List<Vector2Int> AllPassedTiles;
    }

    /// <summary>
    /// 직선 레이저 경로를 계산합니다.
    /// </summary>
    public static LaserLogicResult CalculateLaserPath(Vector2Int startTile, Vector2 direction, int maxRange, System.Func<Vector2Int, bool> isWallCheck, System.Func<Vector2Int, bool> isInsideCheck)
    {
        if (direction.sqrMagnitude < 0.000001f)
        {
            Debug.LogError("[PathCalculationService] direction is zero in CalculateLaserPath");
            return default;
        }

        LaserLogicResult result = new LaserLogicResult
        {
            PassedTiles = new List<Vector2Int>(),
            HitWallTile = Vector2Int.zero,
            HitWall = false
        };

        int safeMaxRange = Mathf.Max(1, maxRange);
        Vector2 normalized = direction.normalized;

        int stepX = normalized.x > 0f ? 1 : (normalized.x < 0f ? -1 : 0);
        int stepY = normalized.y > 0f ? 1 : (normalized.y < 0f ? -1 : 0);

        float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / normalized.x);
        float tMaxY = stepY == 0 ? float.PositiveInfinity : 0.5f / Mathf.Abs(normalized.y);
        float tMaxX = stepX == 0 ? float.PositiveInfinity : 0.5f / Mathf.Abs(normalized.x);
        float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / normalized.y);

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
            if (!isInsideCheck(tile))
            {
                break;
            }

            if (isWallCheck(tile))
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
    /// 리코셰 탄의 튕김 경로를 계산합니다.
    /// </summary>
    public static RicochetLogicResult CalculateRicochetPath(Vector2Int origin, Vector2Int direction, int maxBounces, float stepSize, System.Func<Vector2Int, bool> isWallCheck, System.Func<Vector2Int, bool> isInsideCheck)
    {
        RicochetLogicResult result = new RicochetLogicResult
        {
            Segments = new List<RicochetSegment>(),
            AllPassedTiles = new List<Vector2Int>()
        };

        if (direction == Vector2Int.zero || isInsideCheck == null || isWallCheck == null)
        {
            Debug.LogError("[PathCalculationService] Invalid parameters for RicochetPath calculation");
            return result;
        }

        Vector2 currentPos = origin;
        Vector2 dir = new Vector2(direction.x, direction.y).normalized;
        int bounceCount = 0;
        Vector2Int lastCell = origin;

        for (int step = 0; step < 400; step++)
        {
            currentPos += dir * stepSize;
            Vector2Int currentCell = new Vector2Int(Mathf.RoundToInt(currentPos.x), Mathf.RoundToInt(currentPos.y));

            if (currentCell == lastCell)
            {
                continue;
            }

            if (!isInsideCheck(currentCell) || isWallCheck(currentCell))
            {
                // Segment 완성
                RicochetSegment segment = new RicochetSegment
                {
                    HitWall = true,
                    HitWallTile = currentCell,
                    Direction = new Vector2Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y))
                };

                // 현재 세그먼트의 PassedTiles 수집 (간단히 마지막 셀까지로近似)
                segment.PassedTiles = new List<Vector2Int>();
                
                result.Segments.Add(segment);

                bounceCount++;
                if (bounceCount > maxBounces)
                {
                    return result;
                }

                // Bounce 방향 계산
                dir = ResolveBounceDirection(isInsideCheck, isWallCheck, lastCell, currentCell, dir);
                currentPos = new Vector2(lastCell.x, lastCell.y) + dir * 0.01f;
                currentCell = lastCell;
                continue;
            }

            // 대각 이동 시 중간 셀 누락 방지 판정 (적 감지용)
            Vector2Int delta = currentCell - lastCell;
            if (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1)
            {
                Vector2Int sideCellX = new Vector2Int(lastCell.x + delta.x, lastCell.y);
                Vector2Int sideCellY = new Vector2Int(lastCell.x, lastCell.y + delta.y);

                // Side cell 도 경로에 추가 (적 감지는 BulletSO 에서 처리)
                if (isInsideCheck(sideCellX) && !isWallCheck(sideCellX))
                {
                    if (!result.AllPassedTiles.Contains(sideCellX))
                        result.AllPassedTiles.Add(sideCellX);
                }
                if (isInsideCheck(sideCellY) && !isWallCheck(sideCellY))
                {
                    if (!result.AllPassedTiles.Contains(sideCellY))
                        result.AllPassedTiles.Add(sideCellY);
                }
            }

            if (!result.AllPassedTiles.Contains(currentCell))
            {
                result.AllPassedTiles.Add(currentCell);
            }

            lastCell = currentCell;
        }

        return result;
    }

    private static Vector2 ResolveBounceDirection(System.Func<Vector2Int, bool> isInsideCheck, System.Func<Vector2Int, bool> isWallCheck, Vector2Int lastCell, Vector2Int currentCell, Vector2 dir)
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

        if (!isInsideCheck(currentCell))
        {
            if (currentCell.x < 0 || currentCell.x >= 100) // 임시 그리드 크기 가정
            {
                return new Vector2(-dir.x, dir.y);
            }
            if (currentCell.y < 0 || currentCell.y >= 100)
            {
                return new Vector2(dir.x, -dir.y);
            }
        }

        return -dir;
    }

    /// <summary>
    /// GridManager 의 메서드를 래핑하여 간결하게 호출할 수 있도록 합니다.
    /// </summary>
    public static LaserLogicResult CalculateLaserPath(Vector2Int startTile, Vector2 direction, int maxRange, GridManager grid)
    {
        return CalculateLaserPath(startTile, direction, maxRange, grid.IsWall, grid.IsInside);
    }

    public static RicochetLogicResult CalculateRicochetPath(Vector2Int origin, Vector2Int direction, int maxBounces, float stepSize, GridManager grid)
    {
        return CalculateRicochetPath(origin, direction, maxBounces, stepSize, grid.IsWall, grid.IsInside);
    }
}
