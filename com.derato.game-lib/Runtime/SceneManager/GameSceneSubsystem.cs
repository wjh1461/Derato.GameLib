using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Derato.GameLib
{
    public enum SceneSwitchStatus
    {
        Success,
        AlreadyLoaded,
        Failed,
        CleanupIncomplete
    }

    public readonly struct SceneSwitchResult
    {
        public SceneSwitchStatus Status { get; }
        public Exception Exception { get; }

        public bool IsSuccess => Status == SceneSwitchStatus.Success || Status == SceneSwitchStatus.AlreadyLoaded;

        public SceneSwitchResult(SceneSwitchStatus status, Exception exception = null)
        {
            Status = status;
            Exception = exception;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class GameSceneSubsystem : MonoBehaviour, ISubsystem
    {
        #region Inspector
        [SerializeField]
        private SceneGroup mainSceneGroup;
        [SerializeField]
        private SceneGroup gameSceneGroup;

        [SerializeField]
        private SceneAssetReference transitionScene;       
        #endregion

        public bool IsLoading { get; private set; }

        private SceneGroup prevSceneGroup;
        private SceneGroup loadedSceneGroup;

        private readonly Dictionary<SceneGroup, List<SceneInstance>> loadedScenes = new();

        private readonly SemaphoreSlim switchSemaphore = new(1, 1);

        private void Awake()
        {
            SubsystemLocator.RegisterSubsystem(this);
            Initialize();
        }

        private void OnDestroy()
        {
            SubsystemLocator.UnregisterSubsystem(this);
        }

        public void Initialize()
        {
            Debug.Log($"{GetType().Name} Initialized");
        }

        #region 분리해야할 씬 로드 부분
        public async Awaitable OpenMainMenuAsync()
        {
            await SwitchSceneAsync(mainSceneGroup);
        }

        public async Awaitable OpenGameplaySceneAsync()
        {
            await SwitchSceneAsync(gameSceneGroup);
        }

        public async Awaitable OpenInitSceneAsync(SceneGroup initSceneGroup)
        {
            await SwitchSceneAsync(initSceneGroup);
        }
        #endregion

        public async Awaitable<SceneSwitchResult> SwitchSceneAsync(SceneGroup targetSceneGroup, bool isActiveScene = true)
        {
            if (targetSceneGroup == null)
            {
                return new SceneSwitchResult(SceneSwitchStatus.Failed, new ArgumentNullException(nameof(targetSceneGroup)));
            }

            await switchSemaphore.WaitAsync();

            try
            {
                if (loadedSceneGroup == targetSceneGroup)
                {
                    return new SceneSwitchResult(SceneSwitchStatus.AlreadyLoaded);
                }

                IsLoading = true;
                return await LoadSceneGroupAsync(targetSceneGroup, isActiveScene);                
            }
            finally
            {
                IsLoading = false;
                switchSemaphore.Release();
            }
        }        

        /// <summary>
        /// 단일 씬 그룹 로드
        /// </summary>
        private async Awaitable<SceneSwitchResult> LoadSceneGroupAsync(SceneGroup sceneGroup, bool isActiveScene = false)
        {
            SceneGroup oldSceneGroup = loadedSceneGroup;

            AsyncOperationHandle<SceneInstance> mainHandle = new();
            List <AsyncOperationHandle<SceneInstance>> subSceneHandles = new();

            // 로딩 성공한 씬 인스턴스들
            List<SceneInstance> newSceneInstances = new();
            SceneInstance? transitionSceneInstance = null;
            bool committed = false;

            try
            {
                ValidateSceneGroup(sceneGroup);

                // 트랜지션 씬 로드
                transitionSceneInstance = await LoadTransitionSceneAsync();

                // 트랜지션 씬이 활성화된 프레임을 보장하기 위해 한 프레임 대기
                await Awaitable.NextFrameAsync();

                // 메인 씬을 먼저 로드하고, 그 다음에 서브 씬들을 로드
                // 메인 씬 로드
                mainHandle = Addressables.LoadSceneAsync(sceneGroup.GetMainSceneReference(), LoadSceneMode.Additive);

                SceneInstance mainSceneInstance = await mainHandle.Task;

                if (mainHandle.Status != AsyncOperationStatus.Succeeded)
                {                    
                    throw new InvalidOperationException($"Failed to load main scene: {sceneGroup.name}");
                }

                // 메인 씬을 첫 번째로 추가
                newSceneInstances.Add(mainSceneInstance);

                // 서브 씬의 핸들을 저장
                foreach (SceneAssetReference subSceneRef in sceneGroup.GetSubSceneReferences())
                {
                    subSceneHandles.Add(Addressables.LoadSceneAsync(subSceneRef, LoadSceneMode.Additive));
                }

                // 모든 씬 로드 완료를 기다림
                List<Exception> loadExceptions = new();
                foreach (AsyncOperationHandle<SceneInstance> handle in subSceneHandles)
                {
                    SceneInstance instance = await handle.Task;
                    if (handle.Status != AsyncOperationStatus.Succeeded)
                    {
                        loadExceptions.Add(new InvalidOperationException($"Failed to load sub scene: {handle.DebugName}"));
                        continue;
                    }
                    
                    newSceneInstances.Add(instance);
                }
                
                if (loadExceptions.Count > 0)
                {
                    throw new AggregateException($"Failed to load scene group: {sceneGroup.name}", loadExceptions);
                }

                if (isActiveScene && !SceneManager.SetActiveScene(mainSceneInstance.Scene))
                {
                    throw new InvalidOperationException($"Failed to activate scene: {mainSceneInstance.Scene.name}");
                }

                loadedScenes[sceneGroup] = newSceneInstances;
                loadedSceneGroup = sceneGroup;
                prevSceneGroup = oldSceneGroup;

                committed = true;
            }
            catch (Exception ex) 
            {
                Debug.LogException(ex);

                // 새 그룹을 확정하기 전 실패했다면 로드된 씬을 전부 정리합니다.
                if (!committed)
                {
                    await CleanupInstancesAsync(newSceneInstances);
                    
                    loadedScenes.Remove(sceneGroup);
                    if (loadedSceneGroup == sceneGroup)
                    {
                        loadedSceneGroup = oldSceneGroup;
                    }
                }

                ReleaseFailedHandle(mainHandle);
                foreach (var handle in subSceneHandles)
                {
                    ReleaseFailedHandle(handle);
                }

                return new SceneSwitchResult(SceneSwitchStatus.Failed, ex);
            }
            finally
            {
                if (transitionSceneInstance.HasValue)
                {
                    try
                    {
                        await Addressables.UnloadSceneAsync(transitionSceneInstance.Value, autoReleaseHandle: true).Task;
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogException(cleanupException);
                    }
                }
            }

            if (oldSceneGroup != null && committed)
            {
                await UnloadSceneGroupAsync(oldSceneGroup);
            }

            return new SceneSwitchResult(SceneSwitchStatus.Success);
        }

        private async Awaitable<SceneInstance?> LoadTransitionSceneAsync()
        {
            if (transitionScene == null || !transitionScene.RuntimeKeyIsValid())
            {
                return null;
            }

            SceneInstance sceneInstance = await Addressables.LoadSceneAsync(transitionScene, LoadSceneMode.Additive).Task;

            return sceneInstance;
        }

        private async Awaitable UnloadSceneGroupAsync(SceneGroup sceneGroup)
        {
            if (sceneGroup == null)
            {
                return;
            }

            if (!loadedScenes.ContainsKey(sceneGroup))
            {
                Debug.LogWarning($"Scene group {sceneGroup.name} is not loaded.");
                return;
            }

            List<SceneInstance> currentLoadedScenes = loadedScenes[sceneGroup];

            // 리스트를 역순으로 순회하여 메인 씬이 마지막에 언로드되도록 함
            for (int i = currentLoadedScenes.Count - 1; i >= 0; --i)
            {
                SceneInstance sceneInst = currentLoadedScenes[i];

                if(sceneInst.Scene.IsValid() && sceneInst.Scene.isLoaded)
                {
                    // autoReleaseHandle 옵션이 있는 버전이면 true 권장(핸들 누수 방지)
                    await Addressables.UnloadSceneAsync(sceneInst, true).Task;
                }                                

                currentLoadedScenes.RemoveAt(i);
            }

            loadedScenes.Remove(sceneGroup);

            if(loadedSceneGroup == sceneGroup)
            {
                loadedSceneGroup = null;
            }

            if(prevSceneGroup == sceneGroup)
            {
                prevSceneGroup = null;
            }
        }

        private async Awaitable CleanupInstancesAsync(List<SceneInstance> instances)
        {
            for (int i = instances.Count - 1; i >= 0; --i)
            {
                await TryUnloadSceneAsync(instances[i]);
            }

            instances.Clear();
        }

        private async Awaitable<bool> TryUnloadSceneAsync(SceneInstance instance)
        {
            if (!instance.Scene.IsValid() || !instance.Scene.isLoaded)
            {
                return true;
            }

            try
            {
                await Addressables.UnloadSceneAsync(instance, autoReleaseHandle: true).Task;

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static void ReleaseFailedHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Failed)
            {
                Addressables.Release(handle);
            }
        }

        private static void ValidateSceneGroup(SceneGroup sceneGroup)
        {
            SceneAssetReference mainScene = sceneGroup.GetMainSceneReference();
            if (mainScene == null || !mainScene.RuntimeKeyIsValid())
            {
                throw new InvalidOperationException($"{sceneGroup.name}: Main scene is invalid.");
            }

            List<SceneAssetReference> subScenes = sceneGroup.GetSubSceneReferences();
            if (subScenes == null)
            {
                return;
            }

            HashSet<object> sceneKeys = new() { mainScene.RuntimeKey };

            foreach (SceneAssetReference subScene in subScenes)
            {
                if (subScene == null || !subScene.RuntimeKeyIsValid())
                {
                    throw new InvalidOperationException($"{sceneGroup.name}: Invalid sub scene reference.");
                }

                if (!sceneKeys.Add(subScene.RuntimeKey))
                {
                    throw new InvalidOperationException($"{sceneGroup.name}: Duplicate scene reference.");
                }
            }
        }
    }
}
