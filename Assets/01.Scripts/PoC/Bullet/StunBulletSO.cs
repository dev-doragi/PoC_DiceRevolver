using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StunBullet", menuName = "PoC/Bullets/Stun")]
public class StunBulletSO : BulletLogicSO
{
    [SerializeField] private int _stunTurns = 2;

        public override List<Vector3> Execute(Vector2Int origin, Vector2Int direction, GridManager grid, int damageMultiplier = 1)
        {
            List<Vector3> pathPoints = new List<Vector3>();
            EnemyController enemy = GetFirstEnemyInLine(origin, direction, grid, pathPoints);
            if (enemy == null)
            {
                return pathPoints;
            }

            enemy.TakeDamage(Damage * damageMultiplier);
            enemy.ApplyStun(_stunTurns);

            return pathPoints;
        }
}
