namespace TheOne.UITemplate.Editor.Optimization.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Caching service with Time-To-Live (TTL) support for optimization analysis results.
    /// Prevents redundant asset analysis by caching results for a configurable duration.
    /// </summary>
    public class AssetCache
    {
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();

        /// <summary>
        /// Check if a cached entry is still valid (not expired).
        /// </summary>
        /// <param name="key">The cache key to check</param>
        /// <returns>True if the entry exists and hasn't expired</returns>
        public bool IsValid(string key)
        {
            if (!this.cache.ContainsKey(key))
                return false;

            return this.cache[key].ExpiresAt > DateTime.Now;
        }

        /// <summary>
        /// Get a cached value by key.
        /// WARNING: Call IsValid() first to ensure the cache is not expired!
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">The cache key</param>
        /// <returns>The cached value</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the key doesn't exist</exception>
        /// <exception cref="InvalidCastException">Thrown if the cached type doesn't match T</exception>
        public T Get<T>(string key)
        {
            if (!this.cache.ContainsKey(key))
                throw new KeyNotFoundException($"Cache key '{key}' not found. Call IsValid() first!");

            return (T)this.cache[key].Data;
        }

        /// <summary>
        /// Try to get a cached value by key.
        /// Safer alternative to Get() that handles missing/expired keys gracefully.
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The output value if found and valid</param>
        /// <returns>True if the value was found and is still valid</returns>
        public bool TryGet<T>(string key, out T value)
        {
            if (this.IsValid(key))
            {
                value = (T)this.cache[key].Data;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Set a value in the cache with a Time-To-Live (TTL).
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="data">The data to cache</param>
        /// <param name="duration">How long the cache entry should remain valid</param>
        public void Set(string key, object data, TimeSpan duration)
        {
            this.cache[key] = new CacheEntry
            {
                Data = data,
                ExpiresAt = DateTime.Now + duration,
            };
        }

        /// <summary>
        /// Remove a specific cache entry.
        /// </summary>
        /// <param name="key">The cache key to remove</param>
        /// <returns>True if the entry was removed, false if it didn't exist</returns>
        public bool Remove(string key)
        {
            return this.cache.Remove(key);
        }

        /// <summary>
        /// Clear all cached entries.
        /// </summary>
        public void Clear()
        {
            this.cache.Clear();
        }

        /// <summary>
        /// Remove all expired entries from the cache.
        /// Call this periodically to prevent memory buildup.
        /// </summary>
        public int ClearExpired()
        {
            var now = DateTime.Now;
            var expiredKeys = this.cache
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
                this.cache.Remove(key);

            return expiredKeys.Count;
        }

        /// <summary>
        /// Get statistics about the current cache state.
        /// Useful for debugging and monitoring.
        /// </summary>
        /// <returns>Tuple of (total count, list of cache keys)</returns>
        public (int count, List<string> keys) GetStats()
        {
            return (this.cache.Count, this.cache.Keys.ToList());
        }

        /// <summary>
        /// Get detailed statistics including expiration times.
        /// </summary>
        /// <returns>Dictionary of key → expiration time</returns>
        public Dictionary<string, DateTime> GetDetailedStats()
        {
            return this.cache.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ExpiresAt
            );
        }

        /// <summary>
        /// Get the total memory size estimate of all cached data.
        /// NOTE: This is an approximation and may not be 100% accurate.
        /// </summary>
        /// <returns>Estimated size in bytes</returns>
        public long GetEstimatedSize()
        {
            // Rough estimation: 100 bytes per entry + data size
            // For collections, multiply by count
            long totalSize = 0;

            foreach (var entry in this.cache.Values)
            {
                totalSize += 100; // Base overhead per entry

                if (entry.Data is ICollection<object> collection)
                    totalSize += collection.Count * 64; // Rough estimate per item
            }

            return totalSize;
        }

        /// <summary>
        /// Internal cache entry structure.
        /// </summary>
        private class CacheEntry
        {
            public object Data { get; set; }
            public DateTime ExpiresAt { get; set; }

            public bool IsExpired => DateTime.Now >= this.ExpiresAt;
        }
    }
}
