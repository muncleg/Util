using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;


public class AddressableManager : Singleton<AddressableManager>
{
    private readonly Dictionary<string, object> loadedAssets = new Dictionary<string, object>();

    public async Task<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
    {
        if (loadedAssets.TryGetValue(key, out object loadedAsset))
        {
            return loadedAsset as T;
        }
        else
        {
            try
            {
                AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    loadedAssets[key] = handle.Result;
                    return handle.Result;
                }
                else
                {
                    Debug.LogError($"[AddressableManager] 자산 로드 실패: {key}");
                    return null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AddressableManager] 자산 로드 중 예외 발생: {key}, {e.Message}");
                return null;
            }
        }
    }

    public void ReleaseAsset(string key)
    {
        if (loadedAssets.TryGetValue(key, out object asset))
        {
            Addressables.Release(asset);
            loadedAssets.Remove(key);
        }
    }

    public void ReleaseAllAssets()
    {
        foreach (var asset in loadedAssets.Values)
        {
            Addressables.Release(asset);
        }
        loadedAssets.Clear();
    }
}
