using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetBundleLoadingTools.Utilities
{
    /// <summary>
    /// Extensions to load asset bundles and the assets within them.
    /// </summary>
    public static class AssetBundleExtensions
    {
        /// <summary>
        /// Asynchronously loads an AssetBundle from a file on disk.
        /// </summary>
        /// <param name="path">Path of the file on disk.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<AssetBundle?> LoadFromFileAsync(string path)
        {
            TaskCompletionSource<AssetBundle> taskCompletionSource = new();
            var bundleRequest = AssetBundle.LoadFromFileAsync(path);
            bundleRequest.completed += delegate 
            { 
                taskCompletionSource.SetResult(bundleRequest.assetBundle); 
            };

            return await taskCompletionSource.Task;
        }

        /// <summary>
        /// Asynchronously create an AssetBundle from a memory region.
        /// </summary>
        /// <param name="binary">Array of bytes with the AssetBundle data.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<AssetBundle?> LoadFromMemoryAsync(byte[] binary)
        {
            TaskCompletionSource<AssetBundle> taskCompletionSource = new();
            var bundleRequest = AssetBundle.LoadFromMemoryAsync(binary);
            bundleRequest.completed += delegate
            {
                taskCompletionSource.SetResult(bundleRequest.assetBundle);
            };

            return await taskCompletionSource.Task;
        }

        /// <summary>
        /// Asynchronously loads an AssetBundle from a managed Stream.
        /// </summary>
        /// <param name="stream">The managed Stream object. Unity calls Read(), Seek() and the Length property on this object to load the AssetBundle data.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<AssetBundle?> LoadFromStreamAsync(Stream stream)
        {
            TaskCompletionSource<AssetBundle> taskCompletionSource = new();
            var bundleRequest = AssetBundle.LoadFromStreamAsync(stream);
            bundleRequest.completed += delegate
            {
                taskCompletionSource.SetResult(bundleRequest.assetBundle);
            };

            return await taskCompletionSource.Task;
        }

        /// <summary>
        /// Asynchronously loads asset with name of type <typeparamref name="T"/> from the bundle.
        /// </summary>
        /// <typeparam name="T">The type of the asset to load.</typeparam>
        /// <param name="assetBundle">The asset bundle from which to load the asset.</param>
        /// <param name="path">The path of the asset in the asset bundle.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public static async Task<T?> LoadAssetAsync<T>(AssetBundle assetBundle, string path) where T : Object
        {
            TaskCompletionSource<T> taskCompletionSource = new();
            var assetRequest = assetBundle.LoadAssetAsync<T>(path);
            assetRequest.completed += delegate
            {
                taskCompletionSource.SetResult((T)assetRequest.asset);
            };

            return await taskCompletionSource.Task;
        }
    }
}
