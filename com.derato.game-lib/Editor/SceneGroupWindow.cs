using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Derato.GameLib.Editor
{
    public class SceneGroupWindow : EditorWindow
    {
        private SceneGroup sceneGroup;
        private Vector2 scrollPosition;

        [MenuItem("Tools/SceneGroup Loader")]
        public static void Open()
        {
            GetWindow<SceneGroupWindow>("Scene Group Loader");
        }

        [MenuItem("Assets/Open SceneGroup", true)]
        private static bool CanOpenSelectedSceneGroup()
        {
            return Selection.activeObject is SceneGroup;
        }

        [MenuItem("Assets/Open SceneGroup")]
        private static void OpenSelectedSceneGroup()
        {
            if (Selection.activeObject is SceneGroup selectedSceneGroup)
            {
                OpenSceneGroup(selectedSceneGroup);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SceneGroup", EditorStyles.boldLabel);
            sceneGroup = (SceneGroup)EditorGUILayout.ObjectField(sceneGroup, typeof(SceneGroup), false);

            using (new EditorGUI.DisabledScope(sceneGroup == null))
            {
                if (GUILayout.Button("Open SceneGroup"))
                {
                    OpenSceneGroup(sceneGroup);
                }
            }

            EditorGUILayout.Space(8f);
            DrawSceneList(sceneGroup);
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is SceneGroup selectedSceneGroup)
            {
                sceneGroup = selectedSceneGroup;
                Repaint();
            }
        }

        private static void OpenSceneGroup(SceneGroup targetSceneGroup)
        {
            if (targetSceneGroup == null)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            List<string> scenePaths = GetScenePaths(targetSceneGroup);

            if (scenePaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Scene Group Loader",
                    $"{targetSceneGroup.name} has no valid scenes.",
                    "OK");
                return;
            }

            for (int i = 0; i < scenePaths.Count; ++i)
            {
                OpenSceneMode openMode = i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive;
                EditorSceneManager.OpenScene(scenePaths[i], openMode);
            }

            Scene activeScene = SceneManager.GetSceneByPath(scenePaths[0]);
            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }
        }

        private static List<string> GetScenePaths(SceneGroup targetSceneGroup)
        {
            List<string> scenePaths = new();
            SerializedObject serializedSceneGroup = new(targetSceneGroup);

            SerializedProperty mainScene = serializedSceneGroup.FindProperty("mainScene");
            AddScenePath(scenePaths, mainScene);

            SerializedProperty subScenes = serializedSceneGroup.FindProperty("subScenes");
            if (subScenes == null || !subScenes.isArray)
            {
                return scenePaths;
            }

            for (int i = 0; i < subScenes.arraySize; ++i)
            {
                AddScenePath(scenePaths, subScenes.GetArrayElementAtIndex(i));
            }

            return scenePaths;
        }

        private void DrawSceneList(SceneGroup targetSceneGroup)
        {
            EditorGUILayout.LabelField("Scenes", EditorStyles.boldLabel);

            if (targetSceneGroup == null)
            {
                EditorGUILayout.HelpBox("Select a SceneGroup to preview its scenes.", MessageType.Info);
                return;
            }

            SceneList sceneList = GetSceneList(targetSceneGroup);
            if (sceneList.Count == 0)
            {
                EditorGUILayout.HelpBox("This SceneGroup has no valid scenes.", MessageType.Warning);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (!string.IsNullOrEmpty(sceneList.MainScenePath))
            {
                DrawSceneListItem("Main", sceneList.MainScenePath);
            }

            foreach (string subScenePath in sceneList.SubScenePaths)
            {
                DrawSceneListItem("Sub", subScenePath);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSceneListItem(string label, string scenePath)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(40f));

                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(sceneAsset, typeof(SceneAsset), false);
                }
            }
        }

        private static SceneList GetSceneList(SceneGroup targetSceneGroup)
        {
            SceneList sceneList = new();
            SerializedObject serializedSceneGroup = new(targetSceneGroup);

            SerializedProperty mainScene = serializedSceneGroup.FindProperty("mainScene");
            sceneList.MainScenePath = GetScenePath(mainScene);

            SerializedProperty subScenes = serializedSceneGroup.FindProperty("subScenes");
            if (subScenes == null || !subScenes.isArray)
            {
                return sceneList;
            }

            for (int i = 0; i < subScenes.arraySize; ++i)
            {
                string subScenePath = GetScenePath(subScenes.GetArrayElementAtIndex(i));
                if (!string.IsNullOrEmpty(subScenePath) && !sceneList.SubScenePaths.Contains(subScenePath))
                {
                    sceneList.SubScenePaths.Add(subScenePath);
                }
            }

            return sceneList;
        }

        private static void AddScenePath(List<string> scenePaths, SerializedProperty sceneReference)
        {
            string scenePath = GetScenePath(sceneReference);
            if (string.IsNullOrEmpty(scenePath))
            {
                return;
            }

            if (!scenePaths.Contains(scenePath))
            {
                scenePaths.Add(scenePath);
            }
        }

        private static string GetScenePath(SerializedProperty sceneReference)
        {
            if (sceneReference == null)
            {
                return string.Empty;
            }

            SerializedProperty assetGuid = sceneReference.FindPropertyRelative("m_AssetGUID");
            if (assetGuid == null || string.IsNullOrEmpty(assetGuid.stringValue))
            {
                return string.Empty;
            }

            string scenePath = AssetDatabase.GUIDToAssetPath(assetGuid.stringValue);
            if (string.IsNullOrEmpty(scenePath))
            {
                return string.Empty;
            }

            if (AssetDatabase.GetMainAssetTypeAtPath(scenePath) != typeof(SceneAsset))
            {
                Debug.LogWarning($"{scenePath} is not a scene asset.");
                return string.Empty;
            }

            return scenePath;
        }

        private class SceneList
        {
            public string MainScenePath { get; set; }
            public List<string> SubScenePaths { get; } = new();

            public int Count
            {
                get
                {
                    int mainSceneCount = string.IsNullOrEmpty(MainScenePath) ? 0 : 1;
                    return mainSceneCount + SubScenePaths.Count;
                }
            }
        }
    }
}
