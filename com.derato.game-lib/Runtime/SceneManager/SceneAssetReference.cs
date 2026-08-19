using UnityEngine.AddressableAssets;
using UnityEngine;

namespace Derato.GameLib
{
    [System.Serializable]
    public class SceneAssetReference : AssetReferenceT<Object>
    {
        public SceneAssetReference(string guid) : base(guid)
        {
        }

        public override bool ValidateAsset(Object obj)
        {
#if UNITY_EDITOR
            return obj is UnityEditor.SceneAsset;
#else
            return true;
#endif
        }

        public override bool ValidateAsset(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(UnityEditor.SceneAsset);
#else
            return true;
#endif
        }
    }
}
