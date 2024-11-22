using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private readonly Dictionary<string, Queue<GameObject>> gameObjectPools = new Dictionary<string, Queue<GameObject>>();

    private const int MAX_POOL_SIZE = 50;

    /// <summary>
    /// GameObject를 풀에서 스폰하거나 Addressables를 통해 로드하여 반환합니다.
    /// </summary>
    public async Task<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!gameObjectPools.ContainsKey(key))
        {
            gameObjectPools[key] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = gameObjectPools[key];
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.parent = parent;
            obj.SetActive(true);
        }
        else
        {
            GameObject prefab = await AddressableManager.Instance.LoadAssetAsync<GameObject>(key);
            if (prefab == null)
            {
                Debug.LogError($"[ObjectPoolManager] 프리팹 로드 실패: {key}");
                return null;
            }

            obj = Instantiate(prefab, position, rotation, parent);
        }

        return obj;
    }

    /// <summary>
    /// GameObject를 비활성화하고 풀에 반환합니다.
    /// 풀의 최대 크기를 초과할 경우 메모리를 해제합니다.
    /// </summary>
    public void Despawn(string key, GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[ObjectPoolManager] 디스폰하려는 오브젝트가 null입니다: {key}");
            return;
        }

        if (!gameObjectPools.ContainsKey(key))
        {
            gameObjectPools[key] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = gameObjectPools[key];

        if (pool.Count >= MAX_POOL_SIZE)
        {
            // 풀의 크기가 최대 크기를 초과하면 오브젝트를 파괴하고 메모리를 해제
            Destroy(obj);
            AddressableManager.Instance.ReleaseAsset(key);
            Debug.Log($"[ObjectPoolManager] 풀의 최대 크기({MAX_POOL_SIZE}) 초과로 인해 오브젝트를 파괴했습니다: {key}");
        }
        else
        {
            // 오브젝트를 풀에 반환
            obj.SetActive(false);
            obj.transform.parent = this.transform;
            pool.Enqueue(obj);
        }
    }

    /// <summary>
    /// 특정 풀의 모든 오브젝트를 파괴하고 Addressables에 릴리즈합니다.
    /// </summary>
    public void ClearPool(string key)
    {
        if (gameObjectPools.TryGetValue(key, out Queue<GameObject> pool))
        {
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            gameObjectPools.Remove(key);
        }

        AddressableManager.Instance.ReleaseAsset(key);
    }

    /// <summary>
    /// 모든 풀의 오브젝트를 파괴하고 Addressables에 릴리즈합니다.
    /// </summary>
    public void ClearAll()
    {
        // 디스폰이 선행되어야함.
        // 모든 GameObject 풀 정리
        foreach (var pool in gameObjectPools.Values)
        {
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
        }
        gameObjectPools.Clear();

        AddressableManager.Instance.ReleaseAllAssets();
    }
}
