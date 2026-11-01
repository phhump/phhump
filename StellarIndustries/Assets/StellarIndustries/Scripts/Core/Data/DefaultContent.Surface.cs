using System.Collections.Generic;

namespace StellarIndustries.Core.Data
{
    /// <summary>Footprints and surface appearance for the engineer's walkable surfaces.</summary>
    public static partial class DefaultContent
    {
        static void AddSurfaceData(GameDatabase db)
        {
            var footprints = new Dictionary<string, double>
            {
                ["surface_mine"] = 10, ["oil_derrick"] = 10, ["water_pump"] = 6, ["ice_extractor"] = 10, ["he3_harvester"] = 14, ["atmo_processor"] = 12,
                ["smelter"] = 12, ["refinery"] = 16, ["chemical_plant"] = 16, ["component_factory"] = 14, ["rocket_assembly"] = 30, ["research_lab"] = 12,
                ["wind_turbine"] = 8, ["coal_plant"] = 18, ["solar_array"] = 12, ["fusion_reactor"] = 24, ["warehouse"] = 14, ["laser_array"] = 30,
            };
            foreach (var kv in footprints) { var b = db.Building(kv.Key); if (b != null) b.FootprintM = kv.Value; }

            void Surface(string id, string ground, string sky, double relief, double craters, double half = 300)
            {
                var l = db.Location(id);
                if (l == null) return;
                l.GroundColorHex = ground; l.SkyColorHex = sky; l.TerrainReliefM = relief; l.CraterDensity = craters; l.SurfaceHalfSizeM = half;
            }
            // Sky colours: Rayleigh-blue on Earth, butterscotch dust on Mars, black where there is no air.
            Surface("earth", "#5E6B3C", "#7FB2E6", 7, 0);
            Surface("moon", "#8E8C88", "#000000", 5, 0.9);
            Surface("mars", "#9C5A35", "#C9A27E", 8, 0.35);
            Surface("proxima_b", "#6E5446", "#140C0A", 6, 0.5);
            Surface("proxima_d", "#7A5A44", "#000000", 5, 0.8);
            Surface("barnard_e", "#6B4E40", "#000000", 4, 0.9);
            Surface("eps_eri_b", "#B49A78", "#3A2A1C", 0, 0, 200);   // orbital platform deck above the clouds
            Surface("trappist1_e", "#5E5A66", "#1A0E0C", 6, 0.4);
        }
    }
}
