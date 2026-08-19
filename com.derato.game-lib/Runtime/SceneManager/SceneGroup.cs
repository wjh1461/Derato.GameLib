using System.Collections.Generic;
using UnityEngine;

namespace Derato.GameLib
{
    /// <summary>
    /// [메인 씬 1 + 서브 씬 n] 구조를 하나의 그룹으로 관리하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "NewSceneGroup", menuName = "GameSceneManager/SceneGroup")]
    public class SceneGroup : ScriptableObject
    {
        #region Inspector
        [Header("Main Scene")]
        [SerializeField]
        private SceneAssetReference mainScene;

        [Header("Sub Scenes")]
        [SerializeField]
        private List<SceneAssetReference> subScenes; 
        #endregion

        public SceneAssetReference GetMainSceneReference() => mainScene;
        public List<SceneAssetReference> GetSubSceneReferences() => subScenes;
    }
}
