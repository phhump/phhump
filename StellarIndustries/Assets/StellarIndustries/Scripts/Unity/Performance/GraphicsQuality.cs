using System;
using UnityEngine;

namespace StellarIndustries.Unity.Performance
{
    public enum GfxQuality { Low, Medium, High, Ultra }

    /// <summary>
    /// Device-level graphics settings (stored in PlayerPrefs, not in the save: a save moves between PCs, graphics don't).
    /// Every preset keeps the real planet textures, atmospheres and modern UI; lower presets drop costly extras
    /// (shadows, MSAA, cloud layer, decoration density, particle/drone counts, texture resolution).
    /// </summary>
    public static class GraphicsQuality
    {
        const string KeyLevel = "si_gfx_level", KeyAdaptive = "si_gfx_adaptive", KeyBattery = "si_gfx_battery", KeyFps = "si_gfx_showfps";
        public static GfxQuality Level { get; private set; } = GfxQuality.High;
        public static bool Adaptive = true, BatterySaver = true, ShowFps;
        public static GfxQuality Detected { get; private set; }
        public static string HardwareSummary { get; private set; } = "";
        public static event Action Changed;

        public static void Load()
        {
            Detected = Detect();
            Level = PlayerPrefs.HasKey(KeyLevel) ? (GfxQuality)Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel), 0, 3) : Detected;
            Adaptive = PlayerPrefs.GetInt(KeyAdaptive, 1) == 1;
            BatterySaver = PlayerPrefs.GetInt(KeyBattery, 1) == 1;
            ShowFps = PlayerPrefs.GetInt(KeyFps, 0) == 1;
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KeyLevel, (int)Level);
            PlayerPrefs.SetInt(KeyAdaptive, Adaptive ? 1 : 0);
            PlayerPrefs.SetInt(KeyBattery, BatterySaver ? 1 : 0);
            PlayerPrefs.SetInt(KeyFps, ShowFps ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Set(GfxQuality q)
        {
            if (q == Level) return;
            Level = q;
            Save();
            Apply();
            Changed?.Invoke();
        }

        /// <summary>Picks a preset from the hardware: VRAM, RAM, CPU cores, integrated vs dedicated GPU.</summary>
        public static GfxQuality Detect()
        {
            int vram = SystemInfo.graphicsMemorySize, ram = SystemInfo.systemMemorySize, cores = SystemInfo.processorCount;
            string gpu = SystemInfo.graphicsDeviceName ?? "";
            bool integrated = gpu.Contains("Intel") || gpu.Contains("UHD") || gpu.Contains("Iris") || gpu.Contains("Radeon(TM) Graphics") || gpu.Contains("Vega 8");
            HardwareSummary = $"{gpu} · {vram} MB VRAM · {ram} MB RAM · {cores} threads";
            if (Application.isMobilePlatform || vram < 1100 || ram < 4000 || cores <= 2) return GfxQuality.Low;
            if (integrated || vram < 2500 || ram < 8000) return GfxQuality.Medium;
            if (vram < 7000) return GfxQuality.High;
            return GfxQuality.Ultra;
        }

        public static void Apply()
        {
            var l = Level;
            QualitySettings.shadows = l >= GfxQuality.High ? ShadowQuality.All : l == GfxQuality.Medium ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            QualitySettings.shadowDistance = new[] { 0f, 70f, 140f, 240f }[(int)l];
            QualitySettings.shadowResolution = new[] { ShadowResolution.Low, ShadowResolution.Medium, ShadowResolution.High, ShadowResolution.VeryHigh }[(int)l];
            QualitySettings.shadowCascades = new[] { 1, 1, 2, 4 }[(int)l];
            QualitySettings.antiAliasing = new[] { 0, 2, 4, 8 }[(int)l];
            QualitySettings.anisotropicFiltering = l == GfxQuality.Low ? AnisotropicFiltering.Disable : l == GfxQuality.Medium ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = new[] { 0.6f, 1f, 1.5f, 2.2f }[(int)l];
            QualitySettings.pixelLightCount = new[] { 1, 2, 4, 8 }[(int)l];
            QualitySettings.softParticles = false;
            // Half-resolution textures on Low (8K Earth -> 4K): a quarter of the video memory, still sharp.
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = l == GfxQuality.Low ? 1 : 0;
#else
            QualitySettings.masterTextureLimit = l == GfxQuality.Low ? 1 : 0;
#endif
            QualitySettings.vSyncCount = l == GfxQuality.Low ? 0 : 1;
            Application.targetFrameRate = TargetFps;
        }

        public static int TargetFps => Level == GfxQuality.Low ? 30 : 60;

        // ---- feature budgets used by the views -------------------------------------------
        public static int TerrainGrid => new[] { 100, 150, 200, 256 }[(int)Level];
        public static int HorizonGrid => new[] { 50, 80, 120, 160 }[(int)Level];
        public static int SphereSegments => new[] { 48, 72, 112, 160 }[(int)Level];
        public static float DecorationDensity => new[] { 0.35f, 0.65f, 1f, 1.4f }[(int)Level];
        public static float ParticleBudget => new[] { 0.3f, 0.6f, 1f, 1.5f }[(int)Level];
        public static int MaxDrones => new[] { 6, 12, 24, 40 }[(int)Level];
        public static int MaxFootprints => new[] { 60, 150, 400, 800 }[(int)Level];
        public static bool CloudLayer => Level >= GfxQuality.Medium;
        public static bool Shadows => Level >= GfxQuality.Medium;
        public static bool SoftShadows => Level >= GfxQuality.High;
        /// <summary>Screen-height fraction below which buildings switch to their simple model.</summary>
        public static float LodDetailThreshold => new[] { 0.2f, 0.12f, 0.07f, 0.04f }[(int)Level];
        public static float LodCullThreshold => new[] { 0.02f, 0.012f, 0.006f, 0.003f }[(int)Level];
    }
}
