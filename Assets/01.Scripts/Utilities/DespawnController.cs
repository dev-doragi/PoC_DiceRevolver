using UnityEngine;

/// <summary>
/// 파티클 시스템이 끝나면 자동으로 풀에 반환하는 컴포넌트입니다.
/// </summary>
public class DespawnController : MonoBehaviour
{
    private ParticleSystem _particleSystem;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        if (_particleSystem == null) return;

        if (!_particleSystem.isPlaying)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Despawn(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
