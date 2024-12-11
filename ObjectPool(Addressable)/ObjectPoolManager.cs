using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Linq;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private readonly Dictionary<string, Queue<GameObject>> gameObjectPools = new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<GameObject, float> lastUsedTime = new Dictionary<GameObject, float>();

    private const float CLEANUP_INTERVAL = 10f;     // 몇 초마다 정리
    private const float OBJECT_LIFETIME = 30f;      // 풀에 방치된 오브젝트 유지 시간

    protected void Awake()
    {
        CleanupRoutine().Forget();
    }

    private async UniTaskVoid CleanupRoutine()
    {
        while (true)
        {
            await UniTask.Delay((int)(CLEANUP_INTERVAL * 1000));
            CleanUpOldObjects();
        }
    }

    private void CleanUpOldObjects()
    {
        float now = Time.time;

        // 성능 최적화: LINQ 제거
        List<string> keys = new List<string>(gameObjectPools.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            Queue<GameObject> pool = gameObjectPools[key];
            int poolCount = pool.Count;
            List<GameObject> remaining = new List<GameObject>(poolCount);

            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (lastUsedTime.TryGetValue(obj, out float usedTime))
                {
                    float timeInPool = Time.time - usedTime;
                    if (timeInPool > OBJECT_LIFETIME)
                    {
                        AddressableManager.Instance.ReleaseInstance(obj);
                        lastUsedTime.Remove(obj);
                        // Debug.Log($"[ObjectPoolManager] 오래된 오브젝트 파괴: {obj.name}, Key: {key}");
                    }
                    else
                    {
                        remaining.Add(obj);
                    }
                }
                else
                {
                    // 기록 없으면 지금 기록
                    lastUsedTime[obj] = Time.time;
                    remaining.Add(obj);
                }
            }

            for (int j = 0; j < remaining.Count; j++)
            {
                pool.Enqueue(remaining[j]);
            }
        }
    }

    public async UniTask<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!gameObjectPools.ContainsKey(key))
        {
            gameObjectPools[key] = new Queue<GameObject>();
        }

        Queue<GameObject> pool = gameObjectPools[key];

        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            lastUsedTime.Remove(obj);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.parent = parent;
            obj.SetActive(true);
            return obj;
        }

        GameObject newObj = await AddressableManager.Instance.InstantiateAsync(key, position, rotation, parent);
        if (newObj == null)
        {
            Debug.LogError($"[ObjectPoolManager] 스폰 실패: {key}");
            return null;
        }

        return newObj;
    }

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
        lastUsedTime[obj] = Time.time; // 반환 시간 기록
    }

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
                    lastUsedTime.Remove(obj);
                }
            }
            gameObjectPools.Remove(key);
        }

        // 필요시: AddressableManager.Instance.ReleaseAsset(key);
    }

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
                    lastUsedTime.Remove(obj);
                }
            }
        }

        gameObjectPools.Clear();
        Debug.Log("[ObjectPoolManager] 모든 풀 정리 완료");
    }

    public Dictionary<string, int> GetPoolInfo()
    {
        Dictionary<string, int> info = new Dictionary<string, int>();
        foreach (var kvp in gameObjectPools)
        {
            info[kvp.Key] = kvp.Value.Count;
        }
        return info;
    }

    /// <summary>
    /// 상세 정보: 키별로 오브젝트 리스트(이름, timeInPool, timeUntilCleanup) 반환
    /// </summary>
    public Dictionary<string, List<(string objName, float timeInPool, float timeUntilCleanup)>> GetDetailedPoolInfo()
    {
        Dictionary<string, List<(string, float, float)>> detailedInfo = new Dictionary<string, List<(string, float, float)>>();

        float now = Time.time;
        foreach (var kvp in gameObjectPools)
        {
            string key = kvp.Key;
            Queue<GameObject> pool = kvp.Value;
            // Queue 순회 위해 잠시 ToArray()
            // GC 할당 최소화를 위해 상황에 따라 다른 접근 가능하지만 여기선 단순성 유지
            var arr = pool.ToArray();

            List<(string, float, float)> list = new List<(string, float, float)>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                GameObject obj = arr[i];
                float usedTime;
                if (lastUsedTime.TryGetValue(obj, out usedTime))
                {
                    float timeInPool = now - usedTime;
                    float timeUntilCleanup = OBJECT_LIFETIME - timeInPool;
                    list.Add((obj.name, timeInPool, timeUntilCleanup));
                }
                else
                {
                    // 기록 없는 경우 방금 반환된 것으로 간주
                    list.Add((obj.name, 0f, OBJECT_LIFETIME));
                }
            }

            detailedInfo[key] = list;
        }

        return detailedInfo;
    }
}
