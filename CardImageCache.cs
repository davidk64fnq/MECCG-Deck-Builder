using Microsoft.Extensions.Caching.Memory;
using System;
using System.Drawing;
using System.IO;
using System.Net.Http;

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

        internal Bitmap GetOrCreate(string key, string setFolder, string imageName)
        {
            // 1. Check in-memory cache
            if (Cache.TryGetValue(key, out Bitmap cacheEntry))
            {
                return cacheEntry;
            }

            string filePath = Path.Combine(setFolder, imageName);

            // 2. Fall back to local disk
            if (File.Exists(filePath))
            {
                cacheEntry = LoadImageNonLocking(filePath);
            }
            else
            {
                // 3. Fall back to remote network download
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
                        // Disk write failure should not prevent using the downloaded in-memory image
                    }
                }
            }

            // 4. Populate MemoryCache on disk hit or network download
            if (cacheEntry != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(1);

                Cache.Set(key, cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;
        }

        /// <summary>
        /// Reads image bytes into memory before creating a Bitmap so GDI+ does not lock the file on disk.
        /// </summary>
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
            catch (AggregateException ae)
            {
                if (ae.InnerExceptions.Count > 0)
                {
                    // Log inner exceptions if necessary
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}