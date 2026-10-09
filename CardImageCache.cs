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
        // HttpClient should be a shared, static, or long-lived instance
        private static readonly HttpClient s_httpClient = new();

        private readonly MemoryCache Cache = new(new MemoryCacheOptions()
        {
            SizeLimit = 10000
        });

        #region ASYNC_METHODS

        /// <summary>
        /// Asynchronously retrieves an image from in-memory cache, local disk, or remote Cardnum endpoint.
        /// </summary>
        internal async Task<Bitmap> GetOrCreateAsync(string key, string setFolder, string imageName, CancellationToken cancellationToken = default)
        {
            // 1. Check in-memory cache
            if (Cache.TryGetValue(key, out Bitmap cacheEntry))
            {
                return cacheEntry;
            }

            string filePath = Path.Combine(setFolder, imageName);

            // 2. Fall back to local disk asynchronously
            if (File.Exists(filePath))
            {
                cacheEntry = await LoadImageNonLockingAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // 3. Fall back to remote network download asynchronously
                cacheEntry = await CreateItemAsync(key, cancellationToken).ConfigureAwait(false);

                if (cacheEntry != null && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (!Directory.Exists(setFolder))
                        {
                            Directory.CreateDirectory(setFolder);
                        }
                        cacheEntry.Save(filePath);
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

        /// <summary>
        /// Asynchronously downloads card image bytes and converts them into a non-locking Bitmap.
        /// </summary>
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

        /// <summary>
        /// Reads image bytes into memory asynchronously before creating a Bitmap so GDI+ does not lock the file on disk.
        /// </summary>
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

        internal Bitmap GetOrCreate(string key, string setFolder, string imageName)
        {
            if (Cache.TryGetValue(key, out Bitmap cacheEntry))
            {
                return cacheEntry;
            }

            string filePath = Path.Combine(setFolder, imageName);

            if (File.Exists(filePath))
            {
                cacheEntry = LoadImageNonLocking(filePath);
            }
            else
            {
                cacheEntry = CreateItem(key);

                if (cacheEntry != null)
                {
                    try
                    {
                        if (!Directory.Exists(setFolder))
                        {
                            Directory.CreateDirectory(setFolder);
                        }
                        cacheEntry.Save(filePath);
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