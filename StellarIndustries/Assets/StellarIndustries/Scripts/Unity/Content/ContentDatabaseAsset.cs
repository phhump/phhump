using System.Collections.Generic;
using StellarIndustries.Core.Data;
using UnityEngine;

namespace StellarIndustries.Unity.Content
{
    /// <summary>
    /// Root content asset. Place it at Resources/StellarContent.asset (the editor menu
    /// "Stellar Industries > Export Default Content" does this). Asset entries override
    /// built-in definitions with the same id; with IncludeBuiltInContent you only need to
    /// author what you add or change.
    /// </summary>
    [CreateAssetMenu(menuName = "Stellar Industries/Content Database", fileName = "StellarContent")]
    public class ContentDatabaseAsset : ScriptableObject
    {
        public bool IncludeBuiltInContent = true;
        public bool OverrideStartConditions;
        public StartDef Start = new StartDef();
        public List<ResourceAsset> Resources = new List<ResourceAsset>();
        public List<RecipeAsset> Recipes = new List<RecipeAsset>();
        public List<BuildingAsset> Buildings = new List<BuildingAsset>();
        public List<TechnologyAsset> Technologies = new List<TechnologyAsset>();
        public List<RocketAsset> Rockets = new List<RocketAsset>();
        public List<LocationAsset> Locations = new List<LocationAsset>();
        public List<MissionAsset> Missions = new List<MissionAsset>();
        public List<AchievementAsset> Achievements = new List<AchievementAsset>();

        public GameDatabase Build()
        {
            var builtIn = IncludeBuiltInContent ? DefaultContent.Create() : new GameDatabase();
            var db = new GameDatabase { Start = OverrideStartConditions || !IncludeBuiltInContent ? Start : builtIn.Start };
            Merge(builtIn.Resources, Resources, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Recipes, Recipes, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Buildings, Buildings, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Techs, Technologies, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Rockets, Rockets, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Locations, Locations, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Missions, Missions, a => a.Data, d => d.Id, d => db.Add(d));
            Merge(builtIn.Achievements, Achievements, a => a.Data, d => d.Id, d => db.Add(d));
            return db;
        }

        static void Merge<TAsset, TDef>(List<TDef> builtIn, List<TAsset> assets, System.Func<TAsset, TDef> data,
            System.Func<TDef, string> id, System.Action<TDef> add) where TAsset : Object
        {
            var overrides = new Dictionary<string, TDef>();
            var order = new List<string>();
            foreach (var d in builtIn) { overrides[id(d)] = d; order.Add(id(d)); }
            foreach (var a in assets)
            {
                if (a == null) continue;
                var d = data(a);
                if (d == null || string.IsNullOrEmpty(id(d))) { Debug.LogWarning("[StellarIndustries] Content asset without id: " + a.name, a); continue; }
                if (!overrides.ContainsKey(id(d))) order.Add(id(d));
                overrides[id(d)] = d;
            }
            foreach (var key in order) add(overrides[key]);
        }
    }

    public static class ContentLoader
    {
        public const string ResourcePath = "StellarContent";

        public static GameDatabase Load()
        {
            var asset = UnityEngine.Resources.Load<ContentDatabaseAsset>(ResourcePath);
            var db = asset != null ? asset.Build() : DefaultContent.Create();
            var errors = db.Validate();
            foreach (var e in errors) Debug.LogWarning("[StellarIndustries] Content: " + e);
            Debug.Log($"[StellarIndustries] Content loaded ({(asset != null ? "asset" : "built-in")}): {db.Resources.Count} resources, " +
                      $"{db.Buildings.Count} buildings, {db.Recipes.Count} recipes, {db.Techs.Count} techs, {db.Locations.Count} locations, {errors.Count} problems");
            return db;
        }
    }
}
