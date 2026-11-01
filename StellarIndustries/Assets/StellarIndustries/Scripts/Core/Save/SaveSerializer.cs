using System;
using System.Collections.Generic;
using System.Globalization;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;
using StellarIndustries.Core.Util;

namespace StellarIndustries.Core.Save
{
    public class SaveHeader
    {
        public string Format;
        public int Version;
        public string Checksum;
        public string Name;
        public long SavedUtcTicks;
        public double PlayTime;
        public int Buildings;
        public int Locations;
    }

    public class LoadResult
    {
        public GameState State;
        public SaveHeader Header;
        public string Error;
        public bool UsedBackup;
        public readonly List<string> Warnings = new List<string>();
        public bool Ok => State != null && Error == null;
    }

    /// <summary>
    /// Save file format: line 1 = header JSON (format, version, checksum, summary),
    /// line 2 = state JSON. The checksum (FNV-1a over line 2) detects truncated/corrupted files.
    /// Older versions are migrated step by step on the raw JSON tree before mapping.
    /// </summary>
    public static class SaveSerializer
    {
        public const string Format = "STELLAR-INDUSTRIES-SAVE";
        public const int CurrentVersion = 1;

        /// <summary>Migration steps: index i migrates a tree from version i to i+1.</summary>
        static readonly Dictionary<int, Action<Dictionary<string, object>>> Migrations = new Dictionary<int, Action<Dictionary<string, object>>>
        {
            // Example for future versions:
            // [1] = tree => { /* rename field, convert data */ },
        };

        public static string Serialize(GameState state)
        {
            state.Version = CurrentVersion;
            string body = Json.Serialize(JsonMapper.ToTree(state));
            var header = new Dictionary<string, object>
            {
                ["Format"] = Format,
                ["Version"] = CurrentVersion,
                ["Checksum"] = Fnv1a(body),
                ["Name"] = state.SaveName ?? "",
                ["SavedUtcTicks"] = state.LastSavedUtcTicks.ToString(CultureInfo.InvariantCulture),
                ["PlayTime"] = state.PlayTime,
                ["Buildings"] = state.Buildings.Count,
                ["Locations"] = state.UnlockedLocations.Count,
            };
            return Json.Serialize(header) + "\n" + body;
        }

        public static SaveHeader ReadHeader(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int nl = text.IndexOf('\n');
            if (nl <= 0) return null;
            try
            {
                var h = Json.Deserialize(text.Substring(0, nl)) as Dictionary<string, object>;
                if (h == null) return null;
                var header = JsonMapper.FromTree<SaveHeader>(h);
                if (h.TryGetValue("SavedUtcTicks", out var t) && t is string ts && long.TryParse(ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
                    header.SavedUtcTicks = ticks;
                return header.Format == Format ? header : null;
            }
            catch (Exception) { return null; }
        }

        public static LoadResult Deserialize(string text, GameDatabase db)
        {
            var result = new LoadResult();
            try
            {
                var header = ReadHeader(text);
                if (header == null) { result.Error = "Not a Stellar Industries save file or header damaged"; return result; }
                result.Header = header;
                if (header.Version > CurrentVersion) { result.Error = "Save was created by a newer game version (v" + header.Version + ")"; return result; }
                string body = text.Substring(text.IndexOf('\n') + 1).TrimEnd('\r', '\n');
                if (header.Checksum != Fnv1a(body)) result.Warnings.Add("Checksum mismatch - the save may have been modified or damaged");
                var tree = Json.Deserialize(body) as Dictionary<string, object>;
                if (tree == null) { result.Error = "Save body is not a JSON object"; return result; }
                for (int v = header.Version; v < CurrentVersion; v++)
                    if (Migrations.TryGetValue(v, out var migrate)) { migrate(tree); result.Warnings.Add("Migrated save from v" + v + " to v" + (v + 1)); }
                var state = JsonMapper.FromTree<GameState>(tree);
                if (tree.TryGetValue("CreatedUtcTicks", out var c)) state.CreatedUtcTicks = ParseLong(c);
                state.LastSavedUtcTicks = header.SavedUtcTicks;
                state.Version = CurrentVersion;
                SaveSanitizer.Sanitize(state, db, result.Warnings);
                result.State = state;
            }
            catch (Exception e)
            {
                result.Error = "Corrupted save: " + e.Message;
                result.State = null;
            }
            return result;
        }

        static long ParseLong(object o)
        {
            if (o is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            if (o is double d) return (long)d;
            return 0;
        }

        public static string Fnv1a(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return h.ToString("x8");
        }
    }
}
