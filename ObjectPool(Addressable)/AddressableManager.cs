using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

public class AddressableManager : Singleton<AddressableManager>
{
    /// <summary>
    /// Addressables를 통해 비동기 인스턴스화 후 반환 (UniTask 기반)
    /// </summary>
    public async UniTask<GameObject> InstantiateAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, position, rotation, parent);
        await handle.Task.AsUniTask(); // Task -> UniTask 변환을 통해 await

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            return handle.Result;
        }
        else
        {
            Debug.LogError($"[AddressableManager] InstantiateAsync 실패: {key}");
            return null;
        }
    }

    /// <summary>
    /// Addressables 인스턴스 해제
    /// </summary>
    public void ReleaseInstance(GameObject instance)
    {
        if (instance != null)
        {
            Addressables.ReleaseInstance(instance);
        }
    }
}
