using System;
using System.Collections.Concurrent;

namespace VTStudioToolBox.Helpers
{
    public static class CacheManager
    {
        private static readonly ConcurrentDictionary<string, CacheItem> _cache = new ConcurrentDictionary<string, CacheItem>();

        public static void Set<T>(string key, T? value, TimeSpan? expiration = null)
        {
            var cacheItem = new CacheItem
            {
                Value = value,
                Expiration = expiration.HasValue ? DateTime.Now.Add(expiration.Value) : DateTime.MaxValue,
                Type = typeof(T)
            };
            _cache[key] = cacheItem;
        }

        public static T? Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (DateTime.Now < cacheItem.Expiration)
                {
                    return (T?)cacheItem.Value;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                }
            }
            return default;
        }

        public static bool Contains(string key)
        {
            return _cache.ContainsKey(key);
        }

        public static void Clear()
        {
            _cache.Clear();
        }

        private class CacheItem
        {
            public object? Value { get; set; }
            public DateTime Expiration { get; set; }
            public Type? Type { get; set; }
        }
    }
}