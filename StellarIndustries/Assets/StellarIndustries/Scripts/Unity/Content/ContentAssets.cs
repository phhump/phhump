using StellarIndustries.Core.Data;
using UnityEngine;

// ScriptableObject wrappers around the engine-independent definitions.
// Each asset holds exactly one definition; Unity serializes the [Serializable] fields,
// so designers edit content in the Inspector. Create via Assets > Create > Stellar Industries.
namespace StellarIndustries.Unity.Content
{
    [CreateAssetMenu(menuName = "Stellar Industries/Resource", fileName = "Resource")]
    public class ResourceAsset : ScriptableObject { public ResourceDef Data = new ResourceDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Building", fileName = "Building")]
    public class BuildingAsset : ScriptableObject { public BuildingDef Data = new BuildingDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Recipe", fileName = "Recipe")]
    public class RecipeAsset : ScriptableObject { public RecipeDef Data = new RecipeDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Technology", fileName = "Technology")]
    public class TechnologyAsset : ScriptableObject { public TechDef Data = new TechDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Rocket", fileName = "Rocket")]
    public class RocketAsset : ScriptableObject { public RocketDef Data = new RocketDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Location", fileName = "Location")]
    public class LocationAsset : ScriptableObject { public LocationDef Data = new LocationDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Mission", fileName = "Mission")]
    public class MissionAsset : ScriptableObject { public MissionDef Data = new MissionDef(); }

    [CreateAssetMenu(menuName = "Stellar Industries/Achievement", fileName = "Achievement")]
    public class AchievementAsset : ScriptableObject { public AchievementDef Data = new AchievementDef(); }
}
