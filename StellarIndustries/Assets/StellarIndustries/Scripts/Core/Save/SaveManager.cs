using System;
using System.Collections.Generic;
using System.IO;
using StellarIndustries.Core.Data;
using StellarIndustries.Core.State;

namespace StellarIndustries.Core.Save
{
    public interface ISaveStorage
    {
        bool Exists(string key);
        string Read(string key);
        /// <summary>Returns the previous version of the slot (kept on every write), or null.</summary>
        string ReadBackup(string key);
        void Write(string key, string content);
        void Delete(string key);
    }

    /// <summary>File storage with atomic writes (temp file + replace) and one rolling backup.</summary>
    public class FileSaveStorage : ISaveStorage
    {
        readonly string dir;
        public FileSaveStorage(string directory) { dir = directory; Directory.CreateDirectory(dir); }
        string PathOf(string key) => Path.Combine(dir, key + ".sav");
        public bool Exists(string key) => File.Exists(PathOf(key));
        public string Read(string key) => File.Exists(PathOf(key)) ? File.ReadAllText(PathOf(key)) : null;
        public string ReadBackup(string key) => File.Exists(PathOf(key) + ".bak") ? File.ReadAllText(PathOf(key) + ".bak") : null;
        public void Write(string key, string content)
        {
            string path = PathOf(key), tmp = path + ".tmp", bak = path + ".bak";
            File.WriteAllText(tmp, content);
            if (File.Exists(path))
            {
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            File.Move(tmp, path);
        }
        public void Delete(string key)
        {
            foreach (var p in new[] { PathOf(key), PathOf(key) + ".bak", PathOf(key) + ".tmp" }) if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>In-memory storage for tests.</summary>
    public class MemorySaveStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public bool Exists(string key) => Files.ContainsKey(key);
        public string Read(string key) => Files.TryGetValue(key, out var v) ? v : null;
        public string ReadBackup(string key) => Files.TryGetValue(key + ".bak", out var v) ? v : null;
        public void Write(string key, string content) { if (Files.TryGetValue(key, out var old)) Files[key + ".bak"] = old; Files[key] = content; }
        public void Delete(string key) { Files.Remove(key); Files.Remove(key + ".bak"); }
    }

    public class SlotInfo
    {
        public int Slot;
        public bool Exists;
        public bool Corrupt;
        public SaveHeader Header;
    }

    /// <summary>Multiple save slots, versioned files, backup fallback on corruption.</summary>
    public class SaveManager
    {
        public const int SlotCount = 3;
        readonly ISaveStorage storage;
        readonly GameDatabase db;
        public Func<DateTime> Clock = () => DateTime.UtcNow;

        public SaveManager(ISaveStorage storage, GameDatabase db) { this.storage = storage; this.db = db; }

        public static string Key(int slot) => "slot" + slot;

        public void Save(int slot, GameState state)
        {
            state.LastSavedUtcTicks = Clock().Ticks;
            storage.Write(Key(slot), SaveSerializer.Serialize(state));
        }

        public LoadResult Load(int slot)
        {
            var text = storage.Read(Key(slot));
            if (text == null) return new LoadResult { Error = "Slot " + slot + " is empty" };
            var result = SaveSerializer.Deserialize(text, db);
            if (result.Ok) return result;
            var backup = storage.ReadBackup(Key(slot));
            if (backup != null)
            {
                var b = SaveSerializer.Deserialize(backup, db);
                if (b.Ok)
                {
                    b.UsedBackup = true;
                    b.Warnings.Insert(0, "Main save was unreadable (" + result.Error + "). Restored the backup.");
                    return b;
                }
            }
            return result;
        }

        public SlotInfo Info(int slot)
        {
            var info = new SlotInfo { Slot = slot, Exists = storage.Exists(Key(slot)) };
            if (info.Exists)
            {
                info.Header = SaveSerializer.ReadHeader(storage.Read(Key(slot)));
                info.Corrupt = info.Header == null;
            }
            return info;
        }

        public void Delete(int slot) => storage.Delete(Key(slot));
    }
}
