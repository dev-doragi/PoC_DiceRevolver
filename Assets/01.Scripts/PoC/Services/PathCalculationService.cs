using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 기반 총알 경로 계산 (Straight, Ricochet) 을 담당하는 순수 계산 서비스입니다.
/// GridModel/GridManager 와 독립적으로 동작하며, Preview 와 실제 발사 경로 일치를 보장합니다.
/// </summary>
public static class PathCalculationService
{
    public struct LaserLogicResult
    {
        public List<Vector2Int> PassedTiles;
        public Vector2Int HitWallTile;
        public bool HitWall;
    }

    public struct RicochetLogicResult
    {
        public List<LaserLogicResult> Segments;
        public List<Vector2Int> AllAffectedTiles;
    }

    /// <summary>
    /// 직선 레이저 경로를 계산합니다 (DDA 알고리즘).
    /// </summary>
    public static LaserLogicResult CalculateLaserPath(
        Vector2Int startTile, 
        Vector2 direction, 
        int maxRange,
        System.Func<Vector2Int, bool> isWallCheck,
        System.Func<Vector2Int, bool> isInsideCheck)
    {
        if (direction.sqrMagnitude < 0.000001f)
        {
            Debug.LogError("[PathCalculationService] direction is zero in CalculateLaserPath");
            throw new System.InvalidOperationException("[PathCalculationService] direction is zero in CalculateLaserPath");
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
    /// 리코셰 탄 경로를 계산합니다 (최대 bounces 까지).
    /// </summary>
    public static RicochetLogicResult CalculateRicochetPath(
        Vector2Int origin,
        Vector2Int direction,
        int maxBounces,
        float stepSize,
        System.Func<Vector2Int, bool> isWallCheck,
        System.Func<Vector2Int, bool> isInsideCheck)
    {
        RicochetLogicResult result = new RicochetLogicResult
        {
            Segments = new List<LaserLogicResult>(),
            AllAffectedTiles = new List<Vector2Int>()
        };

        if (direction == Vector2Int.zero)
        {
            return result;
        }

        Vector2 currentPos = origin;
        Vector2 dir = new Vector2(direction.x, direction.y).normalized;
        int bounceCount = 0;
        Vector2Int lastCell = origin;
        HashSet<Vector2Int> visitedTiles = new HashSet<Vector2Int>();

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
                bounceCount++;
                if (bounceCount > maxBounces)
                {
                    break;
                }

                dir = ResolveBounceDirection(lastCell, currentCell, dir, isInsideCheck);
                currentPos = new Vector2(lastCell.x, lastCell.y) + dir * 0.01f;
                currentCell = lastCell;
                continue;
            }

            // 대각 이동 시 중간 셀 누락 방지 판정
            Vector2Int delta = currentCell - lastCell;
            if (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1)
            {
                Vector2Int sideCellX = new Vector2Int(lastCell.x + delta.x, lastCell.y);
                Vector2Int sideCellY = new Vector2Int(lastCell.x, lastCell.y + delta.y);

                if (!visitedTiles.Contains(sideCellX))
                {
                    visitedTiles.Add(sideCellX);
                    result.AllAffectedTiles.Add(sideCellX);
                }

                if (!visitedTiles.Contains(sideCellY))
                {
                    visitedTiles.Add(sideCellY);
                    result.AllAffectedTiles.Add(sideCellY);
                }
            }

            if (!visitedTiles.Contains(currentCell))
            {
                visitedTiles.Add(currentCell);
                result.AllAffectedTiles.Add(currentCell);
            }

            lastCell = currentCell;
        }

        // Segments 구성 (단순화를 위해 전체 타일 목록만 반환)
        var segment = new LaserLogicResult
        {
            PassedTiles = new List<Vector2Int>(result.AllAffectedTiles),
            HitWallTile = Vector2Int.zero,
            HitWall = false
        };
        result.Segments.Add(segment);

        return result;
    }

    /// <summary>
    /// 리코셰 방향 반사를 계산합니다.
    /// </summary>
    private static Vector2 ResolveBounceDirection(
        Vector2Int lastCell, 
        Vector2Int currentCell, 
        Vector2 dir,
        System.Func<Vector2Int, bool> isInsideCheck)
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
            if (currentCell.x < 0 || currentCell.x >= 100) // gridSize 는 외부에서 전달 필요 (임시)
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
    /// 대각 이동 시 관통하는 적 체크를 위한 보조 메서드
    /// </summary>
    public static List<Vector2Int> GetDiagonalCheckCells(Vector2Int lastCell, Vector2Int currentCell)
    {
        List<Vector2Int> checkCells = new List<Vector2Int>();
        Vector2Int delta = currentCell - lastCell;

        if (Mathf.Abs(delta.x) == 1 && Mathf.Abs(delta.y) == 1)
        {
            checkCells.Add(new Vector2Int(lastCell.x + delta.x, lastCell.y));
            checkCells.Add(new Vector2Int(lastCell.x, lastCell.y + delta.y));
        }

        return checkCells;
    }
}
