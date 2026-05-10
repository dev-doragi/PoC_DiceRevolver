using System;
using System.Collections.Generic;
using UnityEngine;

public static class PathCalculationService
{
    public struct LaserPathResult
    {
        public List<Vector2Int> PassedTiles;
        public Vector2Int HitWallTile;
        public bool HitWall;
    }

    public struct RicochetPathResult
    {
        public List<Vector2Int> PassedTiles;
        public Vector2Int HitWallTile;
        public bool HitWall;
        public int BounceCount;
    }

    public static LaserPathResult CalculateLaserPath(
        Vector2Int origin,
        Vector2Int direction,
        int maxRange,
        Func<Vector2Int, bool> isInside,
        Func<Vector2Int, bool> isWall)
    {
        LaserPathResult result = new LaserPathResult
        {
            PassedTiles = new List<Vector2Int>(),
            HitWallTile = Vector2Int.zero,
            HitWall = false
        };

        if (direction == Vector2Int.zero)
        {
            Debug.LogError("[PathCalculationService] direction is zero in CalculateLaserPath");
            return result;
        }

        if (isInside == null || isWall == null)
        {
            Debug.LogError("[PathCalculationService] callbacks are null in CalculateLaserPath");
            return result;
        }

        int safeMaxRange = Mathf.Max(1, maxRange);
        Vector2 normalized = new Vector2(direction.x, direction.y).normalized;

        int stepX = normalized.x > 0f ? 1 : (normalized.x < 0f ? -1 : 0);
        int stepY = normalized.y > 0f ? 1 : (normalized.y < 0f ? -1 : 0);

        float tDeltaX = stepX == 0 ? float.PositiveInfinity : Mathf.Abs(1f / normalized.x);
        float tDeltaY = stepY == 0 ? float.PositiveInfinity : Mathf.Abs(1f / normalized.y);

        float tMaxX = stepX == 0 ? float.PositiveInfinity : 0.5f / Mathf.Abs(normalized.x);
        float tMaxY = stepY == 0 ? float.PositiveInfinity : 0.5f / Mathf.Abs(normalized.y);

        int x = origin.x;
        int y = origin.y;

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
            if (!isInside(tile))
            {
                break;
            }

            if (isWall(tile))
            {
                result.HitWall = true;
                result.HitWallTile = tile;
                return result;
            }

            result.PassedTiles.Add(tile);
        }

        return result;
    }

    public static RicochetPathResult CalculateRicochetPath(
        Vector2Int origin,
        Vector2Int direction,
        int maxBounces,
        int maxStepsPerBounce,
        Func<Vector2Int, bool> isInside,
        Func<Vector2Int, bool> isWall)
    {
        RicochetPathResult result = new RicochetPathResult
        {
            PassedTiles = new List<Vector2Int>(),
            HitWallTile = Vector2Int.zero,
            HitWall = false,
            BounceCount = 0
        };

        if (direction == Vector2Int.zero)
        {
            Debug.LogError("[PathCalculationService] direction is zero in CalculateRicochetPath");
            return result;
        }

        if (isInside == null || isWall == null)
        {
            Debug.LogError("[PathCalculationService] callbacks are null in CalculateRicochetPath");
            return result;
        }

        Vector2Int currentCell = origin;
        Vector2Int currentDirection = new Vector2Int(
            direction.x == 0 ? 0 : (direction.x > 0 ? 1 : -1),
            direction.y == 0 ? 0 : (direction.y > 0 ? 1 : -1));

        int safeMaxBounces = Mathf.Max(0, maxBounces);
        int safeMaxSteps = Mathf.Max(1, maxStepsPerBounce);

        while (result.BounceCount <= safeMaxBounces && currentDirection != Vector2Int.zero)
        {
            bool bouncedThisSegment = false;

            for (int step = 0; step < safeMaxSteps; step++)
            {
                Vector2Int nextCell = currentCell + currentDirection;
                if (!isInside(nextCell))
                {
                    return result;
                }

                if (isWall(nextCell))
                {
                    result.HitWall = true;
                    result.HitWallTile = nextCell;
                    result.BounceCount++;

                    if (result.BounceCount > safeMaxBounces)
                    {
                        return result;
                    }

                    currentDirection = ResolveBounceDirection(currentDirection);
                    bouncedThisSegment = true;
                    break;
                }

                currentCell = nextCell;
                result.PassedTiles.Add(currentCell);
            }

            if (!bouncedThisSegment)
            {
                return result;
            }
        }

        return result;
    }

    private static Vector2Int ResolveBounceDirection(Vector2Int currentDirection)
    {
        if (Mathf.Abs(currentDirection.x) >= Mathf.Abs(currentDirection.y))
        {
            return new Vector2Int(-currentDirection.x, currentDirection.y);
        }

        return new Vector2Int(currentDirection.x, -currentDirection.y);
    }
}
