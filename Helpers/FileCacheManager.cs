using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace VTStudioToolBox.Helpers
{
    public static class FileCacheManager
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VTStudioToolBox",
            "Cache");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        static FileCacheManager()
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        public static void Set<T>(string key, T value, TimeSpan expiration)
        {
            try
            {
                var cacheItem = new CacheItem<T>
                {
                    Data = value,
                    ExpirationTime = DateTime.Now.Add(expiration)
                };

                string filePath = GetCacheFilePath(key);
                string json = JsonSerializer.Serialize(cacheItem, JsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex) { Logger.Warn("FileCacheManager", $"Set failed: {ex.Message}"); }
        }

        public static T? Get<T>(string key) where T : class
        {
            try
            {
                string filePath = GetCacheFilePath(key);
                if (!File.Exists(filePath))
                    return null;

                string json = File.ReadAllText(filePath);
                var cacheItem = JsonSerializer.Deserialize<CacheItem<T>>(json, JsonOptions);

                if (cacheItem == null)
                    return null;

                if (DateTime.Now > cacheItem.ExpirationTime)
                {
                    File.Delete(filePath);
                    return null;
                }

                return cacheItem.Data;
            }
            catch (Exception ex) { Logger.Warn("FileCacheManager", $"Get failed: {ex.Message}"); return null; }
        }

        public static bool Exists(string key)
        {
            try
            {
                string filePath = GetCacheFilePath(key);
                if (!File.Exists(filePath))
                    return false;

                string json = File.ReadAllText(filePath);
                var cacheItem = JsonSerializer.Deserialize<CacheItem<JsonElement>>(json, JsonOptions);
                return cacheItem != null && DateTime.Now <= cacheItem.ExpirationTime;
            }
            catch (Exception ex) { Logger.Warn("FileCacheManager", $"Exists failed: {ex.Message}"); return false; }
        }

        public static void Clear()
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(CacheDirectory))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex) { Logger.Warn("FileCacheManager", $"Clear failed: {ex.Message}"); }
        }

        private static string GetCacheFilePath(string key)
        {
            string safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(CacheDirectory, $"{safeKey}.json");
        }

        private class CacheItem<T>
        {
            public T? Data { get; set; }
            public DateTime ExpirationTime { get; set; }
        }
    }
}
