
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
            if (!Cache.TryGetValue(key, out Bitmap cacheEntry))// Look for cache key.
            {
                if (!File.Exists($"{Path.Combine(setFolder, imageName)}"))
                {
                    // Card not in cache or stored on disk so retrieve from Cardnum
                    cacheEntry = CreateItem(key);

                    if (cacheEntry != null)
                    {
                        // Store card locally on disk as well
                        if (!Directory.Exists(setFolder)){
                            Directory.CreateDirectory(setFolder);
                        }
                        cacheEntry.Save($"{Path.Combine(setFolder, imageName)}");

                        var cacheEntryOptions = new MemoryCacheEntryOptions()
                            .SetSize(1);

                        // Save data in cache.
                        Cache.Set(key, cacheEntry, cacheEntryOptions);
                    }
                }
                else
                {
                    // Card not in cache but is on disk so get it from there
                    Bitmap card = new($"{Path.Combine(setFolder, imageName)}");
                    return card;
                }
            }
            return cacheEntry;
        }

        internal static Bitmap CreateItem(string key)
        {
            try
            {
                // 1. Synchronously get the image data as a byte array by blocking
                //    the current thread using .Result. This is what makes it non-async.
                //    .Result is placed inside a try/catch to handle exceptions gracefully.
                byte[] imageBytes = s_httpClient.GetByteArrayAsync(key).Result;

                // 2. Convert the byte array into a memory stream
                using var ms = new MemoryStream(imageBytes);
                using var img = Image.FromStream(ms);   // temporary Image that uses the stream
                return new Bitmap(img);                 // clone into a Bitmap that doesn't depend on the stream
            }
            catch (AggregateException ae)
            {
                // When using .Result, exceptions are often wrapped in an AggregateException.
                // Check for specific inner exceptions if needed (e.g., HttpRequestException)
                if (ae.InnerExceptions.Count > 0)
                {
                    // You can log ae.InnerExceptions[0] for details
                }
                return null;
            }
            catch (Exception)
            {
                // Catch other exceptions (e.g., ArgumentException for invalid URI,
                // or errors during Bitmap construction)
                return null;
            }
        }
    }
}
