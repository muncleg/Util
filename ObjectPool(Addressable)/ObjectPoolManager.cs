using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Linq;

public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    private Dictionary<string, Queue<GameObject>> gameObjectPools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<GameObject, float> lastUsedTime = new Dictionary<GameObject, float>();

    [Header("Settings")]
    [SerializeField] private float cleanupInterval = 10f;
    [SerializeField] private float objectLifetime = 30f;

    [Header("Debug / Test")]
    [SerializeField] private bool testModeOn = false;

    // 테스트 모드 전용 : 컨테이너 관리
    private Dictionary<string, GameObject> t_ContainerDict;

    private void Awake()
    {
        t_ContainerDict = new Dictionary<string, GameObject>();
        CleanupRoutine().Forget();
    }

    private async UniTaskVoid CleanupRoutine()
    {
        while (true)
        {
            await UniTask.Delay((int)(cleanupInterval * 1000));
            CleanUpOldObjects();
        }
    }

    private void CleanUpOldObjects()
    {
        float now = Time.time;
        var keys = gameObjectPools.Keys.ToList();

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
                    float timeInPool = now - usedTime;
                    if (timeInPool > objectLifetime)
                    {
                        AddressableManager.Instance.ReleaseInstance(obj);
                        lastUsedTime.Remove(obj);
                    }
                    else
                    {
                        remaining.Add(obj);
                    }
                }
                else
                {
                    lastUsedTime[obj] = Time.time;
                    remaining.Add(obj);
                }
            }

            for (int j = 0; j < remaining.Count; j++)
            {
                pool.Enqueue(remaining[j]);
            }

            if (testModeOn)
            {
                UpdateContainerName(key);
            }
        }
    }

    public async UniTask<GameObject> SpawnAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!gameObjectPools.ContainsKey(key))
        {
            gameObjectPools[key] = new Queue<GameObject>();
            if (testModeOn) CreateContainerForKey(key);
        }

        var pool = gameObjectPools[key];

        GameObject obj;
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            lastUsedTime.Remove(obj);
        }
        else
        {
            GameObject newObj = await AddressableManager.Instance.InstantiateAsync(key, position, rotation, parent);
            if (newObj == null)
            {
                Debug.LogError($"[ObjectPoolManager] 스폰 실패: {key}");
                return null;
            }

            obj = newObj;
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.transform.parent = parent;
        obj.SetActive(true);

        if (testModeOn)
        {
            SetParentToContainer(key, obj);
            UpdateContainerName(key);
        }

        return obj;
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
            if (testModeOn) CreateContainerForKey(key);
        }

        obj.SetActive(false);
        obj.transform.parent = this.transform;
        gameObjectPools[key].Enqueue(obj);
        lastUsedTime[obj] = Time.time;

        if (testModeOn)
        {
            UpdateContainerName(key);
        }
    }

    public void Despawn(string key, GameObject obj, float delay)
    {
        if (delay <= 0f)
        {
            Despawn(key, obj);
        }
        else
        {
            DespawnDelayed(key, obj, delay).Forget();
        }
    }

    private async UniTaskVoid DespawnDelayed(string key, GameObject obj, float delay)
    {
        await UniTask.Delay((int)(delay * 1000));
        if (obj == null) return;
        if (obj.activeSelf)
        {
            Despawn(key, obj);
        }
    }

    public void ClearPool(string key)
    {
        if (gameObjectPools.TryGetValue(key, out var pool))
        {
            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                {
                    AddressableManager.Instance.ReleaseInstance(obj);
                    lastUsedTime.Remove(obj);
                }
            }
            gameObjectPools.Remove(key);

            if (testModeOn) RemoveContainer(key);
        }
    }

    public void ClearAll()
    {
        foreach (var kvp in gameObjectPools)
        {
            var pool = kvp.Value;
            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                {
                    AddressableManager.Instance.ReleaseInstance(obj);
                    lastUsedTime.Remove(obj);
                }
            }
        }

        gameObjectPools.Clear();
        Debug.Log("[ObjectPoolManager] 모든 풀 정리 완료");

        if (testModeOn) ClearAllContainers();
    }


    #region  에디터 용 함수
    public Dictionary<string, int> GetPoolInfo()
    {
        Dictionary<string, int> info = new Dictionary<string, int>();
        foreach (var kvp in gameObjectPools)
        {
            info[kvp.Key] = kvp.Value.Count;
        }
        return info;
    }

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
        foreach (var kvp in gameObjectPools)
        {
            string key = kvp.Key;
            var pool = kvp.Value.ToArray();
            List<DetailedObjectInfo> list = new List<DetailedObjectInfo>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
            {
                GameObject obj = pool[i];
                float usedTime;
                lastUsedTime.TryGetValue(obj, out usedTime);

                float timeInPool = (usedTime == 0f) ? 0f : now - usedTime;
                float timeUntilCleanup = objectLifetime - timeInPool;
                if (timeUntilCleanup < 0f) timeUntilCleanup = 0f;

                DetailedObjectInfo infoItem = new DetailedObjectInfo
                {
                    objName = obj.name,
                    timeInPool = timeInPool,
                    timeUntilCleanup = timeUntilCleanup
                };
                list.Add(infoItem);
            }

            result[key] = list;
        }

        return result;
    }

    public async UniTask Preload(string key, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var obj = await SpawnAsync(key, Vector3.zero, Quaternion.identity, this.transform);
            if (obj != null)
            {
                Despawn(key, obj);
            }
        }

        Debug.Log($"[ObjectPoolManager] Preload 완료: {key}, {count}개");
    }

    private void CreateContainerForKey(string key)
    {
        if (!testModeOn) return;
        if (t_ContainerDict.ContainsKey(key)) return;
        var container = new GameObject($"[PoolContainer] {key}");
        t_ContainerDict[key] = container;
        UpdateContainerName(key);
    }

    private void SetParentToContainer(string key, GameObject obj)
    {
        if (!testModeOn) return;
        if (t_ContainerDict.TryGetValue(key, out GameObject container))
        {
            obj.transform.SetParent(container.transform);
        }
        UpdateContainerName(key);
    }

    private void UpdateContainerName(string key)
    {
        if (!testModeOn) return;
        if (t_ContainerDict.TryGetValue(key, out GameObject container))
        {
            int totalCount = 0;
            if (gameObjectPools.TryGetValue(key, out var pool))
                totalCount = pool.Count;

            container.name = $"[PoolContainer] {key} : {totalCount} in pool";
        }
    }

    private void RemoveContainer(string key)
    {
        if (!testModeOn) return;
        if (t_ContainerDict.TryGetValue(key, out GameObject container))
        {
            Destroy(container);
            t_ContainerDict.Remove(key);
        }
    }

    private void ClearAllContainers()
    {
        if (!testModeOn) return;
        foreach (var kvp in t_ContainerDict)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        t_ContainerDict.Clear();
    }
    #endregion
}
