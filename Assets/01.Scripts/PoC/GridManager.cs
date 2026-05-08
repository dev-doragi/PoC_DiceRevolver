using System.Collections.Generic;
using UnityEngine;

namespace PocDiceTactics
{
    /// <summary>
    /// 그리드 맵과 장애물, 타일 과열 상태를 관리합니다.
    /// </summary>
    public class GridManager : Singleton<GridManager>
    {
        [Header("Grid")]
        [SerializeField] private Vector2Int _gridSize = new Vector2Int(5, 5);
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector2 _worldOrigin = Vector2.zero;

        [Header("Wall Spawn")]
        [SerializeField] private int _wallCount = 4;
        [SerializeField] private int _maxWallSpawnAttempts = 10;

        [Header("Overheat")]
        [SerializeField] private int _maxOverheatTrail = 2;

        private readonly HashSet<Vector2Int> _walls = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _overheatTrail = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, MonoBehaviour> _occupants = new Dictionary<Vector2Int, MonoBehaviour>();

        public Vector2Int GridSize => _gridSize;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false; // Scene scope
            base.Awake();
        }

        protected override void OnBootstrap()
        {
            GenerateGrid();
        }

        public void GenerateGrid()
        {
            GenerateGrid(Vector2Int.zero);
        }

        public void GenerateGrid(Vector2Int protectedPlayerCell)
        {
            _walls.Clear();
            _overheatTrail.Clear();
            _occupants.Clear();

            GenerateWalls(protectedPlayerCell);
        }

        private void GenerateWalls(Vector2Int protectedPlayerCell)
        {
            int attempts = 0;
            while (attempts < _maxWallSpawnAttempts)
            {
                HashSet<Vector2Int> candidateWalls = new HashSet<Vector2Int>();

                List<Vector2Int> possibleCells = new List<Vector2Int>();
                for (int y = 0; y < _gridSize.y; y++)
                {
                    for (int x = 0; x < _gridSize.x; x++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        possibleCells.Add(cell);
                    }
                }

                for (int i = 0; i < Mathf.Min(_wallCount, possibleCells.Count); i++)
                {
                    int index = Random.Range(0, possibleCells.Count);
                    Vector2Int candidate = possibleCells[index];
                    if (candidate != protectedPlayerCell)
                    {
                        candidateWalls.Add(candidate);
                    }
                    possibleCells.RemoveAt(index);
                }

                if (IsValidWallConfiguration(candidateWalls, protectedPlayerCell))
                {
                    _walls.UnionWith(candidateWalls);
                    break;
                }

                attempts++;
            }

            if (_walls.Count == 0 && _wallCount > 0)
            {
                Debug.LogWarning("[GridManager] Valid wall configuration not found, proceeding without walls");
            }
        }

        private bool IsValidWallConfiguration(HashSet<Vector2Int> walls, Vector2Int playerCell)
        {
            if (walls.Contains(playerCell)) return false;

            List<Vector2Int> nonWallCells = new List<Vector2Int>();
            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int x = 0; x < _gridSize.x; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!walls.Contains(cell))
                    {
                        nonWallCells.Add(cell);
                    }
                }
            }

            if (nonWallCells.Count < 4) return false;

            HashSet<Vector2Int> reachable = FloodFill(playerCell, walls);
            if (reachable.Count != nonWallCells.Count) return false;

            int spawnCandidateCount = 0;
            for (int i = 0; i < nonWallCells.Count; i++)
            {
                if (nonWallCells[i] != playerCell)
                {
                    spawnCandidateCount++;
                }
            }

            if (spawnCandidateCount < 3) return false;

            return true;
        }

        private HashSet<Vector2Int> FloodFill(Vector2Int start, HashSet<Vector2Int> walls)
        {
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                foreach (Vector2Int dir in directions)
                {
                    Vector2Int next = current + dir;
                    if (IsInside(next) && !walls.Contains(next) && !visited.Contains(next))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            return visited;
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _gridSize.x && cell.y >= 0 && cell.y < _gridSize.y;
        }

        public bool IsWall(Vector2Int cell)
        {
            return _walls.Contains(cell);
        }

        public bool DestroyWall(Vector2Int cell)
        {
            if (_walls.Contains(cell))
            {
                _walls.Remove(cell);
                return true;
            }

            return false;
        }

        public bool IsOverheated(Vector2Int cell)
        {
            return _overheatTrail.Contains(cell);
        }

        public bool IsWalkable(Vector2Int cell, MonoBehaviour mover = null)
        {
            if (!IsInside(cell)) return false;
            if (IsWall(cell)) return false;

            if (_occupants.TryGetValue(cell, out MonoBehaviour occupant))
            {
                return occupant == mover;
            }

            return true;
        }

        public bool RegisterOccupant(Vector2Int cell, MonoBehaviour actor)
        {
            if (!IsWalkable(cell, actor)) return false;
            _occupants[cell] = actor;
            return true;
        }

        public void UnregisterOccupant(Vector2Int cell, MonoBehaviour actor)
        {
            if (_occupants.TryGetValue(cell, out MonoBehaviour current) && current == actor)
            {
                _occupants.Remove(cell);
                if (actor is PlayerController)
                {
                    AddOverheat(cell);
                }
            }
        }

        public bool TryMoveOccupant(Vector2Int from, Vector2Int to, MonoBehaviour actor)
        {
            if (!IsWalkable(to, actor)) return false;

            _occupants[to] = actor;
            _occupants.Remove(from);
            if (actor is PlayerController)
            {
                AddOverheat(from);
            }
            return true;
        }

        private void AddOverheat(Vector2Int cell)
        {
            if (_overheatTrail.Contains(cell))
            {
                _overheatTrail.Remove(cell);
            }

            _overheatTrail.Add(cell);
            EventBus.Instance?.Publish(new TileOverheatedEvent { Cell = cell });

            while (_overheatTrail.Count > _maxOverheatTrail)
            {
                Vector2Int cooled = _overheatTrail[0];
                _overheatTrail.RemoveAt(0);
                EventBus.Instance?.Publish(new TileCooledEvent { Cell = cooled });
            }
        }

        public Vector2Int GetRandomEmptyCell()
        {
            List<Vector2Int> candidates = new List<Vector2Int>();

            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int x = 0; x < _gridSize.x; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (IsWalkable(cell))
                    {
                        candidates.Add(cell);
                    }
                }
            }

            if (candidates.Count == 0) return Vector2Int.zero;
            return candidates[Random.Range(0, candidates.Count)];
        }

        public MonoBehaviour GetOccupant(Vector2Int cell)
        {
            _occupants.TryGetValue(cell, out MonoBehaviour actor);
            return actor;
        }

        public IEnumerable<Vector2Int> GetBresenhamLine(Vector2Int origin, Vector2Int delta, int maxDistance = 100)
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
                if (!IsInside(next))
                {
                    yield break;
                }

                yield return next;
            }
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            float centerOffsetX = (_gridSize.x - 1) * 0.5f;
            float centerOffsetY = (_gridSize.y - 1) * 0.5f;
            float x = _worldOrigin.x + ((cell.x - centerOffsetX) * _cellSize);
            float y = _worldOrigin.y + ((cell.y - centerOffsetY) * _cellSize);
            return new Vector3(x, y, 0f);
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            float safeCellSize = Mathf.Max(0.0001f, _cellSize);
            float centerOffsetX = (_gridSize.x - 1) * 0.5f;
            float centerOffsetY = (_gridSize.y - 1) * 0.5f;
            int x = Mathf.RoundToInt(((worldPosition.x - _worldOrigin.x) / safeCellSize) + centerOffsetX);
            int y = Mathf.RoundToInt(((worldPosition.y - _worldOrigin.y) / safeCellSize) + centerOffsetY);
            return new Vector2Int(x, y);
        }
    }
}