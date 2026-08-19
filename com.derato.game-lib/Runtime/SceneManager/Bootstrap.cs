using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Derato.GameLib
{
    /// <summary>
    /// Initializes addressables, loads the persistent scene, and opens the initial scene group.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        #region Inspector
        [SerializeField]
        private SceneAssetReference persistentSceneAsset;

        [Header("Default")]
        [SerializeField]
        private SceneGroup initSceneGroup;
        #endregion

        private async Awaitable Start()
        {
            DontDestroyOnLoad(gameObject);

            CancellationToken ct = destroyCancellationToken;
            await InitializeAsync(ct);

            Destroy(gameObject);
        }

        private async Awaitable InitializeAsync(CancellationToken ct)
        {
            await Addressables.InitializeAsync().Task;

            if (!persistentSceneAsset.RuntimeKeyIsValid())
            {
                Debug.LogError("Persistent scene reference is invalid.");
                return;
            }

            await Addressables.LoadSceneAsync(persistentSceneAsset, LoadSceneMode.Single).Task;

            await SubsystemLocator.InitializeAllAsync(ct);

            await SubsystemLocator.GetSubsystem<GameSceneSubsystem>().OpenInitSceneAsync(initSceneGroup);
        }
    }
}
