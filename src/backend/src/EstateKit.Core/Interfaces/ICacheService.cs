using System.Threading.Tasks;

namespace EstateKit.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for high-performance, distributed caching operations with support for 
    /// generic types, configurable TTL, and async execution.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Retrieves a cached item by key asynchronously with support for distributed cache scenarios.
        /// </summary>
        /// <typeparam name="T">The type of the cached item.</typeparam>
        /// <param name="key">The unique identifier for the cached item.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the cached
        /// item of type T if found, null otherwise.
        /// </returns>
        /// <remarks>
        /// This method checks both local and distributed cache layers for optimal performance.
        /// </remarks>
        Task<T?> GetAsync<T>(string key);

        /// <summary>
        /// Stores an item in the cache asynchronously with configurable expiration and distributed cache support.
        /// </summary>
        /// <typeparam name="T">The type of the item to cache.</typeparam>
        /// <param name="key">The unique identifier for the cached item.</param>
        /// <param name="value">The item to cache.</param>
        /// <param name="expiration">Optional. The time-to-live for the cached item. If not specified, default TTL of 15 minutes is used.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains true if the
        /// item was cached successfully across all cache layers; otherwise, false.
        /// </returns>
        /// <remarks>
        /// This method ensures consistency across both local and distributed cache layers.
        /// </remarks>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// Removes an item from all cache layers asynchronously with distributed cache support.
        /// </summary>
        /// <param name="key">The unique identifier of the item to remove.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains true if the
        /// item was removed successfully from all cache layers; otherwise, false.
        /// </returns>
        /// <remarks>
        /// This method ensures removal propagates across all cache layers for consistency.
        /// </remarks>
        Task<bool> RemoveAsync(string key);

        /// <summary>
        /// Checks if an item exists in any cache layer asynchronously with distributed cache support.
        /// </summary>
        /// <param name="key">The unique identifier of the item to check.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains true if the
        /// item exists in any cache layer; otherwise, false.
        /// </returns>
        /// <remarks>
        /// This method checks both local and distributed cache layers for existence.
        /// </remarks>
        Task<bool> ExistsAsync(string key);
    }
}