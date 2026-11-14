using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceFarm.Editor
{
    public static class SpaceFarmSceneSetup
    {
        [MenuItem("Space Farm/Crear o actualizar escena inicial")]
        public static void CreateScene()
        {
            const string source = "Assets/Scenes/SampleScene.unity";
            const string destination = "Assets/Scenes/SpaceFarm.unity";

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            var scene = EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>(destination) != null
                    ? destination : source,
                OpenSceneMode.Single);
            if (GameObject.Find("Space Farm Game") == null)
            {
                GameObject root = new GameObject("Space Farm Game");
                root.AddComponent<SpaceFarmGame>();
            }
            EditorSceneManager.SaveScene(scene, destination);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(destination, true) };
            PlayerSettings.companyName = "Equipo Space Farm";
            PlayerSettings.productName = "Space Farm";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            AssetDatabase.SaveAssets();
            Debug.Log("SPACE_FARM_SCENE_READY: " + destination);
        }
    }
}
