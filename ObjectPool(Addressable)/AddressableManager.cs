using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

/// <summary>
/// Addressables를 통해 에셋 로딩 및 인스턴스화 기능을 제공하는 매니저.
/// 동기/비동기, 제네릭/비제네릭 형태의 8가지 액션 모두 지원.
/// 
/// Load : 
/// - LoadAsset(string key) : 동기 GameObject 로드 (대용량 비권장)
/// - LoadAssetAsync(string key) : 비동기 GameObject 로드
/// - LoadAsset<T>(string key) : 동기 제네릭 로드 (대용량 비권장)
/// - LoadAssetAsync<T>(string key) : 비동기 제네릭 로드
///
/// Instantiate :
/// - Instantiate(string key, ...) : 동기 GameObject 인스턴스화 (대용량 비권장)
/// - InstantiateAsync(string key, ...) : 비동기 GameObject 인스턴스화
/// - Instantiate<T>(string key, ...) : 동기 제네릭 인스턴스화 (대용량 비권장)
/// - InstantiateAsync<T>(string key, ...) : 비동기 제네릭 인스턴스화
///
/// 모든 동기 메서드는 WaitForCompletion 사용으로 프레임 정지 발생 가능.
/// 가능한 비동기 메서드 활용 권장.
/// </summary>
public class AddressableManager : Singleton<AddressableManager>
{
    //-------------------------------------------------------------------------------------
    // Load (비제네릭, 동기)
    //-------------------------------------------------------------------------------------
    public GameObject LoadAsset(string key)
    {
        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<GameObject>(key);
            GameObject result = handle.WaitForCompletion();
            if (result == null)
            {
                Debug.LogError($"[AddressableManager] LoadAsset 실패: {key}");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddressableManager] LoadAsset Error {key}: {e.Message}");
            return null;
        }
        finally
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }

    //-------------------------------------------------------------------------------------
    // Load (비제네릭, 비동기)
    //-------------------------------------------------------------------------------------
    public async UniTask<GameObject> LoadAssetAsync(string key)
    {
        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<GameObject>(key);
            await handle.Task.AsUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }
            Debug.LogError($"[AddressableManager] LoadAssetAsync 실패: {key}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddressableManager] LoadAssetAsync Error {key}: {e.Message}");
            return null;
        }
        finally
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }

    //-------------------------------------------------------------------------------------
    // Load (제네릭, 동기)
    //-------------------------------------------------------------------------------------
    public T LoadAsset<T>(string key) where T : UnityEngine.Object
    {
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
            T result = handle.WaitForCompletion();
            if (result == null)
            {
                Debug.LogError($"[AddressableManager] LoadAsset<{typeof(T).Name}> 실패: {key}");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddressableManager] LoadAsset<{typeof(T).Name}> Error {key}: {e.Message}");
            return null;
        }
        finally
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }

    //-------------------------------------------------------------------------------------
    // Load (제네릭, 비동기)
    //-------------------------------------------------------------------------------------
    public async UniTask<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
    {
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
            await handle.Task.AsUniTask();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }

            Debug.LogError($"[AddressableManager] LoadAssetAsync<{typeof(T).Name}> 실패: {key}");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AddressableManager] LoadAssetAsync<{typeof(T).Name}> Error {key}: {e.Message}");
            return null;
        }
        finally
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }

    //-------------------------------------------------------------------------------------
    // Instantiate (비제네릭, 동기)
    //-------------------------------------------------------------------------------------
    public GameObject Instantiate(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, position, rotation, parent);
        GameObject result = handle.WaitForCompletion();

        if (result == null)
        {
            Debug.LogError($"[AddressableManager] Instantiate(동기) 실패: {key}");
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }

        return result;
    }

    //-------------------------------------------------------------------------------------
    // Instantiate (비제네릭, 비동기 - 기존 기능)
    //-------------------------------------------------------------------------------------
    public async UniTask<GameObject> InstantiateAsync(string key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, position, rotation, parent);
        await handle.Task.AsUniTask();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject obj = handle.Result;
            return obj;
        }
        else
        {
            Debug.LogError($"[AddressableManager] InstantiateAsync 실패: {key}");
            return null;
        }
    }

    //-------------------------------------------------------------------------------------
    // Instantiate (제네릭, 동기)
    //-------------------------------------------------------------------------------------
    public T Instantiate<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, position, rotation, parent);
        GameObject result = handle.WaitForCompletion();

        if (result == null)
        {
            Debug.LogError($"[AddressableManager] Instantiate<T>(동기) 실패: {key}");
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }

        T component = result.GetComponent<T>();
        if (component != null)
        {
            return component;
        }
        else
        {
            Debug.LogError($"[AddressableManager] Instantiate<T>(동기) {key}: {typeof(T).Name} 컴포넌트 없음");
            Addressables.ReleaseInstance(result);
            return null;
        }
    }

    //-------------------------------------------------------------------------------------
    // Instantiate (제네릭, 비동기)
    //-------------------------------------------------------------------------------------
    public async UniTask<T> InstantiateAsync<T>(string key, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, position, rotation, parent);
        await handle.Task.AsUniTask();

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject result = handle.Result;
            T component = result.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
            else
            {
                Debug.LogError($"[AddressableManager] InstantiateAsync<T> {key}: {typeof(T).Name} 컴포넌트 없음");
                Addressables.ReleaseInstance(result);
                return null;
            }
        }

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }

        Debug.LogError($"[AddressableManager] InstantiateAsync<T> 실패: {key}");
        return null;
    }

    //-------------------------------------------------------------------------------------
    // ReleaseInstance
    //-------------------------------------------------------------------------------------
    public void ReleaseInstance(GameObject instance)
    {
        if (instance != null)
        {
            Addressables.ReleaseInstance(instance);
        }
    }
}
