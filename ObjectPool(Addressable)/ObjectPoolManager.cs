using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private readonly Dictionary<string, Queue<GameObject>> gameObjectPools = new Dictionary<string, Queue<GameObject>>();

    /// <summary>
    /// 풀에서 오브젝트를 가져오거나, 없으면 InstantiateAsync로 새 오브젝트 생성 (UniTask)
    /// </summary>
    public async UniTask<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!gameObjectPools.ContainsKey(key))
        {
            gameObjectPools[key] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = gameObjectPools[key];

        // 풀에 오브젝트가 있으면 재사용
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.parent = parent;
            obj.SetActive(true);
            return obj;
        }

        // 풀에 오브젝트가 없으면 새로 인스턴스 생성 (AddressableManager 사용)
        GameObject newObj = await AddressableManager.Instance.InstantiateAsync(key, position, rotation, parent);
        return newObj;
    }

    /// <summary>
    /// 오브젝트 풀에 반환
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

        obj.SetActive(false);
        obj.transform.parent = this.transform;
        gameObjectPools[key].Enqueue(obj);
    }

    /// <summary>
    /// 특정 풀 정리
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
                    AddressableManager.Instance.ReleaseInstance(obj);
                }
            }
            gameObjectPools.Remove(key);
        }
    }

    /// <summary>
    /// 모든 풀 정리
    /// </summary>
    public void ClearAll()
    {
        foreach (var kvp in gameObjectPools)
        {
            var pool = kvp.Value;
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null)
                {
                    AddressableManager.Instance.ReleaseInstance(obj);
                }
            }
        }

        gameObjectPools.Clear();
    }
}
