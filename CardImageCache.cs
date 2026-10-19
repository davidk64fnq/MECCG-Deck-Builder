using Microsoft.Extensions.Caching.Memory;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MECCG_Deck_Builder
{
    public class CardImageCache
    {
        private static readonly HttpClient s_httpClient = new();

        private readonly MemoryCache Cache = new(new MemoryCacheOptions()
        {
            SizeLimit = 10000
        });

        #region ASYNC_METHODS

        /// <summary>
        /// Asynchronously retrieves an image from in-memory cache, local disk, or remote Cardnum endpoint.
        /// </summary>
        internal async Task<Bitmap> GetOrCreateAsync(string key, string setCode, string imageName, CancellationToken cancellationToken = default)
        {
            // 1. Check in-memory cache
            if (Cache.TryGetValue(key, out Bitmap cacheEntry))
            {
                return cacheEntry;
            }

            string readPath = StoragePathService.ResolveImageReadPath(setCode, imageName);

            // 2. Fall back to local disk asynchronously
            if (File.Exists(readPath))
            {
                cacheEntry = await LoadImageNonLockingAsync(readPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // 3. Fall back to remote network download asynchronously
                cacheEntry = await CreateItemAsync(key, cancellationToken).ConfigureAwait(false);

                if (cacheEntry != null && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        string writePath = StoragePathService.GetCardImagePath(setCode, imageName);
                        string setFolder = Path.GetDirectoryName(writePath);
                        if (!Directory.Exists(setFolder))
                        {
                            Directory.CreateDirectory(setFolder);
                        }
                        cacheEntry.Save(writePath);
                    }
                    catch (Exception)
                    {
                        // Disk write failure should not prevent using the downloaded in-memory image
                    }
                }
            }

            // 4. Populate MemoryCache on disk hit or network download
            if (cacheEntry != null && !cancellationToken.IsCancellationRequested)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(1);

                Cache.Set(key, cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;
        }

        internal static async Task<Bitmap> CreateItemAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] imageBytes = await s_httpClient.GetByteArrayAsync(key, cancellationToken).ConfigureAwait(false);
                using var ms = new MemoryStream(imageBytes);
                using var img = Image.FromStream(ms);
                return new Bitmap(img);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<Bitmap> LoadImageNonLockingAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms);
                return new Bitmap(img);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        #region SYNCHRONOUS_METHODS

        internal Bitmap GetOrCreate(string key, string setCode, string imageName)
        {
            if (Cache.TryGetValue(key, out Bitmap cacheEntry))
            {
                return cacheEntry;
            }

            string readPath = StoragePathService.ResolveImageReadPath(setCode, imageName);

            if (File.Exists(readPath))
            {
                cacheEntry = LoadImageNonLocking(readPath);
            }
            else
            {
                cacheEntry = CreateItem(key);

                if (cacheEntry != null)
                {
                    try
                    {
                        string writePath = StoragePathService.GetCardImagePath(setCode, imageName);
                        string setFolder = Path.GetDirectoryName(writePath);
                        if (!Directory.Exists(setFolder))
                        {
                            Directory.CreateDirectory(setFolder);
                        }
                        cacheEntry.Save(writePath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            if (cacheEntry != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(1);

                Cache.Set(key, cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;
        }

        private static Bitmap LoadImageNonLocking(string filePath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms);
                return new Bitmap(img);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static Bitmap CreateItem(string key)
        {
            try
            {
                byte[] imageBytes = s_httpClient.GetByteArrayAsync(key).Result;
                using var ms = new MemoryStream(imageBytes);
                using var img = Image.FromStream(ms);
                return new Bitmap(img);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion
    }
}