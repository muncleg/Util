using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class PoolData
{
    public string Key;
    public int InitialCount = 10;
    public int MaxCount = 50;
    public float MaxLifetime = 30f;
}

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    [SerializeField] private float cleanupInterval = 10f;
    [SerializeField] private bool testModeOn = false;

    private Dictionary<string, PoolData> poolDataDict = new Dictionary<string, PoolData>();
    private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<GameObject, float> lastUsedTime = new Dictionary<GameObject, float>();

    private Dictionary<string, GameObject> containerDict;

    private void Awake()
    {
        containerDict = new Dictionary<string, GameObject>();
        CleanupRoutine().Forget();
    }

    /// <summary>
    /// 새로운 풀을 최초로 등록하는 메서드.
    /// 해당 키가 이미 존재하면 경고 로그 출력.
    /// </summary>
    public void Register(string key, int initialCount = 0, int maxCount = 50, float maxLifetime = 30f)
    {
        if (poolDataDict.ContainsKey(key))
        {
            Debug.LogWarning($"[ObjectPoolManager] Register 실패: 이미 {key} 키에 대한 PoolData 존재. UpdatePoolData를 사용하세요.");
            return;
        }

        PoolData data = new PoolData
        {
            Key = key,
            InitialCount = initialCount,
            MaxCount = maxCount,
            MaxLifetime = maxLifetime
        };

        poolDataDict[key] = data;
        pools[key] = new Queue<GameObject>();

        if (testModeOn) CreateContainerForKey(key);

        Preload(key, initialCount).Forget();
    }

    /// <summary>
    /// 이미 등록된 풀의 정책 변경
    /// (MaxCount나 MaxLifetime 변경)
    /// </summary>
    public void UpdatePoolData(string key, int maxCount, float maxLifetime)
    {
        if (!poolDataDict.TryGetValue(key, out var data))
        {
            Debug.LogWarning($"[ObjectPoolManager] UpdatePoolData 실패: {key} 키에 대한 PoolData 없음. 먼저 Register 호출 필요.");
            return;
        }

        data.MaxCount = maxCount;
        data.MaxLifetime = maxLifetime;
        poolDataDict[key] = data;

        // 정책 변경 후 정리
        CleanUpOldObjects();
    }

    public async UniTask<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        // 키가 없으면 기본 정책으로 Register
        if (!poolDataDict.ContainsKey(key))
        {
            Debug.Log($"[ObjectPoolManager] SpawnAsync: {key} 키 PoolData 없음. 기본값으로 Register");
            Register(key, initialCount: 0, maxCount: 50, maxLifetime: 30f);
        }

        if (!pools.ContainsKey(key))
        {
            pools[key] = new Queue<GameObject>();
            if (testModeOn) CreateContainerForKey(key);
        }

        var pool = pools[key];
        GameObject obj;

        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            lastUsedTime.Remove(obj);
        }
        else
        {
            var loadedPrefab = await AddressableManager.Instance.LoadAssetAsync<GameObject>(key);
            if (loadedPrefab == null)
            {
                Debug.LogError($"[ObjectPoolManager] SpawnAsync 실패: 프리팹 로드 실패 {key}");
                return null;
            }

            // 인스턴스화
            var objHandle = Addressables.InstantiateAsync(key, position, rotation, parent);
            await objHandle.Task.AsUniTask();
            if (objHandle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded || objHandle.Result == null)
            {
                Debug.LogError($"[ObjectPoolManager] SpawnAsync 실패: 인스턴스화 실패 {key}");
                return null;
            }
            obj = objHandle.Result;
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.transform.parent = parent;
        obj.name = key;
        obj.SetActive(true);

        if (testModeOn) SetParentToContainer(key, obj);

        return obj;
    }

    public void Despawn(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[ObjectPoolManager] Despawn: null obj");
            return;
        }

        string key = obj.name;

        if (!pools.ContainsKey(key))
        {
            // 해당 키에 대한 풀 미등록 상태
            Debug.LogWarning($"[ObjectPoolManager] Despawn 실패: {key} 키에 대한 풀 미등록 상태. 오브젝트 파괴(내 풀로 관리 안 함)");
            Destroy(obj);
            return;
        }

        obj.SetActive(false);
        obj.transform.parent = this.transform;
        pools[key].Enqueue(obj);
        lastUsedTime[obj] = Time.time;

        if (testModeOn) UpdateContainerName(key);
    }

    public void Despawn(string key, GameObject obj, float delay)
    {
        if (delay <= 0f)
        {
            Despawn(obj);
        }
        else
        {
            DespawnDelayed(key, obj, delay).Forget();
        }
    }

    private async UniTaskVoid DespawnDelayed(string key, GameObject obj, float delay)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(delay));
        if (obj != null && obj.activeSelf)
        {
            Despawn(obj);
        }
    }

    public async UniTask Preload(string key, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var obj = await SpawnAsync(key, Vector3.zero, Quaternion.identity, transform);
            if (obj != null)
            {
                Despawn(obj);
            }
        }

        Debug.Log($"[ObjectPoolManager] Preload 완료: {key}, {count}개");
    }

    private async UniTaskVoid CleanupRoutine()
    {
        while (true)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(cleanupInterval));
            CleanUpOldObjects();
        }
    }

    private void CleanUpOldObjects()
    {
        float now = Time.time;
        foreach (var key in pools.Keys.ToList())
        {
            var pool = pools[key];
            if (!poolDataDict.TryGetValue(key, out var poolData)) continue;

            int currentCount = pool.Count;

            // 최대 개수 초과 처리
            while (currentCount > poolData.MaxCount)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                {
                    Addressables.ReleaseInstance(obj);
                    lastUsedTime.Remove(obj);
                    currentCount--;
                }
            }

            // 생명주기 초과 처리
            var toRemove = new List<GameObject>();
            foreach (var obj in pool)
            {
                if (lastUsedTime.TryGetValue(obj, out float usedTime))
                {
                    if (now - usedTime > poolData.MaxLifetime)
                    {
                        toRemove.Add(obj);
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                var newQueue = new Queue<GameObject>(pool.Where(o => !toRemove.Contains(o)));
                pools[key] = newQueue;

                foreach (var obj in toRemove)
                {
                    Addressables.ReleaseInstance(obj);
                    lastUsedTime.Remove(obj);
                }
            }

            if (testModeOn) UpdateContainerName(key);
        }
    }

    #region TestMode
    private void CreateContainerForKey(string key)
    {
        if (!testModeOn) return;
        if (containerDict == null) containerDict = new Dictionary<string, GameObject>();
        if (!containerDict.ContainsKey(key))
        {
            var c = new GameObject($"[PoolContainer] {key}");
            containerDict[key] = c;
            UpdateContainerName(key);
        }
    }

    private void SetParentToContainer(string key, GameObject obj)
    {
        if (!testModeOn) return;
        if (containerDict.TryGetValue(key, out var c))
        {
            obj.transform.SetParent(c.transform);
            UpdateContainerName(key);
        }
    }

    private void UpdateContainerName(string key)
    {
        if (!testModeOn) return;
        if (!containerDict.TryGetValue(key, out var c)) return;

        int count = pools[key].Count;
        int maxCount = poolDataDict[key].MaxCount;
        c.name = $"[PoolContainer] {key} : {count} / {maxCount}";
    }

    private void RemoveContainer(string key)
    {
        if (!testModeOn) return;
        if (containerDict.TryGetValue(key, out var c))
        {
            Destroy(c);
            containerDict.Remove(key);
        }
    }

    [System.Serializable]
    public struct DetailedObjectInfo
    {
        public string objName;
        public float timeInPool;
        public float timeUntilCleanup;
    }

    public Dictionary<string, List<DetailedObjectInfo>> GetDetailedPoolInfo()
    {
        Dictionary<string, List<DetailedObjectInfo>> result = new Dictionary<string, List<DetailedObjectInfo>>();

        float now = Time.time;
        foreach (var kvp in pools)
        {
            string key = kvp.Key;
            var pool = kvp.Value.ToArray();
            List<DetailedObjectInfo> list = new List<DetailedObjectInfo>(pool.Length);

            if (!poolDataDict.TryGetValue(key, out var poolData))
            {
                continue;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                GameObject obj = pool[i];
                if (lastUsedTime.TryGetValue(obj, out float usedTime))
                {
                    float timeInPool = now - usedTime;
                    float timeUntilCleanup = poolData.MaxLifetime - timeInPool;
                    if (timeUntilCleanup < 0f) timeUntilCleanup = 0f;

                    DetailedObjectInfo infoItem = new DetailedObjectInfo
                    {
                        objName = obj.name,
                        timeInPool = timeInPool,
                        timeUntilCleanup = timeUntilCleanup
                    };
                    list.Add(infoItem);
                }
                else
                {
                    DetailedObjectInfo infoItem = new DetailedObjectInfo
                    {
                        objName = obj.name,
                        timeInPool = 0f,
                        timeUntilCleanup = poolData.MaxLifetime
                    };
                    list.Add(infoItem);
                }
            }

            result[key] = list;
        }

        return result;
    }
    #endregion
}
