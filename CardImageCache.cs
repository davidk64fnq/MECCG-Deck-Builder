
using System.Drawing;
using System.IO;
using Microsoft.Extensions.Caching.Memory;

namespace MECCG_Deck_Builder
{
    public class CardImageCache
    {
        private readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions()
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
                    Bitmap card = new Bitmap($"{Path.Combine(setFolder, imageName)}");
                    return card;
                }
            }
            return cacheEntry;
        }

        internal Bitmap CreateItem(string key)
        {
            System.Net.WebRequest request =
            System.Net.WebRequest.Create(key);
            System.Net.WebResponse response = request.GetResponse();
            System.IO.Stream responseStream = response.GetResponseStream();
            if (responseStream != null)
            {
                Bitmap card = new Bitmap(responseStream);
                return card;
            }
            return null;
        }
    }
}
