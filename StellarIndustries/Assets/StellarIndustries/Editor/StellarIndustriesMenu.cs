using System.IO;
using StellarIndustries.Core.Data;
using StellarIndustries.Unity;
using StellarIndustries.Unity.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarIndustries.EditorTools
{
    /// <summary>Editor menu: scene setup, content export to ScriptableObjects and validation.</summary>
    public static class StellarIndustriesMenu
    {
        const string Root = "Assets/StellarIndustries";
        const string ContentRoot = Root + "/Content";
        const string DatabasePath = Root + "/Resources/StellarContent.asset";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Stellar Industries/Create Main Scene", priority = 1)]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Stellar Industries").AddComponent<GameController>();
            EnsureFolder(Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[StellarIndustries] Created " + ScenePath + " and added it to the build settings. Press Play.");
        }

        [MenuItem("Stellar Industries/Export Built-in Content to Assets", priority = 20)]
        public static void ExportContent()
        {
            if (File.Exists(DatabasePath) && !EditorUtility.DisplayDialog("Export content",
                    "A content database already exists. Overwrite all exported content assets with the built-in defaults?", "Overwrite", "Cancel"))
                return;
            var db = DefaultContent.Create();
            var dbAsset = ScriptableObject.CreateInstance<ContentDatabaseAsset>();
            dbAsset.IncludeBuiltInContent = false;
            dbAsset.OverrideStartConditions = true;
            dbAsset.Start = db.Start;
            foreach (var d in db.Resources) dbAsset.Resources.Add(Save<ResourceAsset>("Resources", d.Id, a => a.Data = d));
            foreach (var d in db.Recipes) dbAsset.Recipes.Add(Save<RecipeAsset>("Recipes", d.Id, a => a.Data = d));
            foreach (var d in db.Buildings) dbAsset.Buildings.Add(Save<BuildingAsset>("Buildings", d.Id, a => a.Data = d));
            foreach (var d in db.Techs) dbAsset.Technologies.Add(Save<TechnologyAsset>("Technologies", d.Id, a => a.Data = d));
            foreach (var d in db.Rockets) dbAsset.Rockets.Add(Save<RocketAsset>("Rockets", d.Id, a => a.Data = d));
            foreach (var d in db.Locations) dbAsset.Locations.Add(Save<LocationAsset>("Locations", d.Id, a => a.Data = d));
            foreach (var d in db.Missions) dbAsset.Missions.Add(Save<MissionAsset>("Missions", d.Id, a => a.Data = d));
            foreach (var d in db.Achievements) dbAsset.Achievements.Add(Save<AchievementAsset>("Achievements", d.Id, a => a.Data = d));
            EnsureFolder(Root + "/Resources");
            AssetDatabase.DeleteAsset(DatabasePath);
            AssetDatabase.CreateAsset(dbAsset, DatabasePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = dbAsset;
            Debug.Log("[StellarIndustries] Exported content to " + ContentRoot + " and " + DatabasePath + ". The game now loads content from these assets.");
        }

        static T Save<T>(string folder, string id, System.Action<T> fill) where T : ScriptableObject
        {
            string dir = ContentRoot + "/" + folder;
            EnsureFolder(dir);
            string path = dir + "/" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                fill(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                fill(asset);
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }

        [MenuItem("Stellar Industries/Validate Content", priority = 21)]
        public static void Validate()
        {
            var db = ContentLoader.Load();
            var errors = db.Validate();
            EditorUtility.DisplayDialog("Stellar Industries content",
                errors.Count == 0 ? "Content is valid.\n\n" + db.Resources.Count + " resources, " + db.Recipes.Count + " recipes, " + db.Buildings.Count + " buildings, " + db.Techs.Count + " technologies."
                                  : errors.Count + " problems:\n\n" + string.Join("\n", errors.GetRange(0, Mathf.Min(25, errors.Count))), "OK");
        }

        [MenuItem("Stellar Industries/Open Save Folder", priority = 40)]
        public static void OpenSaves() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
