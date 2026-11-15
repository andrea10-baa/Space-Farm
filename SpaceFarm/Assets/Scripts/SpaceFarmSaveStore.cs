using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpaceFarm
{
    [Serializable]
    public sealed class SpaceFarmSaveData
    {
        public const int CurrentVersion = 2;
        public int Version = CurrentVersion;
        public string Id;
        public string Name;
        public string SavedAtUtc;
        public int Seed;
        public int RoverX, RoverY, TargetX, TargetY;
        public bool[] Revealed;
        public SpaceFarmState.SaveSnapshot State;
    }

    /// <summary>Versioned local save slots. Each write is prepared beside the destination and renamed into place.</summary>
    public static class SpaceFarmSaveStore
    {
        private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "SpaceFarmSaves");

        public static List<SpaceFarmSaveData> List()
        {
            Directory.CreateDirectory(DirectoryPath);
            List<SpaceFarmSaveData> result = new List<SpaceFarmSaveData>();
            foreach (string path in Directory.GetFiles(DirectoryPath, "*.json"))
            {
                try
                {
                    SpaceFarmSaveData data = JsonUtility.FromJson<SpaceFarmSaveData>(File.ReadAllText(path));
                    if (data != null && data.Version == SpaceFarmSaveData.CurrentVersion && !string.IsNullOrEmpty(data.Id)) result.Add(data);
                }
                catch (Exception exception) { Debug.LogWarning($"No se pudo leer el guardado {Path.GetFileName(path)}: {exception.Message}"); }
            }
            result.Sort((a, b) => string.CompareOrdinal(b.SavedAtUtc, a.SavedAtUtc));
            return result;
        }

        public static bool NameExists(string name, string exceptId = null)
        {
            string normalized = NormalizeName(name);
            foreach (SpaceFarmSaveData data in List())
                if (data.Id != exceptId && NormalizeName(data.Name) == normalized) return true;
            return false;
        }

        public static string NormalizeName(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

        public static void Write(SpaceFarmSaveData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.Name))
                throw new ArgumentException("El guardado necesita ID y nombre.");
            if (data.Version != SpaceFarmSaveData.CurrentVersion) throw new InvalidOperationException("Versión de guardado no compatible.");
            Directory.CreateDirectory(DirectoryPath);
            string destination = Path.Combine(DirectoryPath, data.Id + ".json");
            string temporary = destination + ".tmp";
            data.SavedAtUtc = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(data, true);
            using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream)) { writer.Write(json); writer.Flush(); stream.Flush(true); }
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }

        public static SpaceFarmSaveData Read(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            string path = Path.Combine(DirectoryPath, Path.GetFileName(id) + ".json");
            if (!File.Exists(path)) return null;
            SpaceFarmSaveData data = JsonUtility.FromJson<SpaceFarmSaveData>(File.ReadAllText(path));
            return data != null && data.Version == SpaceFarmSaveData.CurrentVersion ? data : null;
        }

        public static bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string path = Path.Combine(DirectoryPath, Path.GetFileName(id) + ".json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }
}
