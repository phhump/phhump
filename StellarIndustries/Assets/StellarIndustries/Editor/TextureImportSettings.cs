using UnityEditor;

namespace StellarIndustries.EditorTools
{
    /// <summary>
    /// Import settings for the HD planet textures: keep full resolution (Unity's default max size is 2048),
    /// repeat horizontally for seamless longitude wrapping, use the cloud map's brightness as transparency.
    /// </summary>
    public class TextureImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("StellarIndustries/Resources/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 8192;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.wrapModeU = UnityEngine.TextureWrapMode.Repeat;
            ti.wrapModeV = UnityEngine.TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.EndsWith("earth_clouds.jpg"))
            {
                ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                ti.alphaIsTransparency = true;
            }
        }

        [MenuItem("Stellar Industries/Reimport HD Textures", priority = 41)]
        static void Reimport() => AssetDatabase.ImportAsset("Assets/StellarIndustries/Resources/Textures", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
    }
}
