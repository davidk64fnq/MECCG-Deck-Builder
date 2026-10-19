using System;
using System.IO;

namespace MECCG_Deck_Builder
{
    public static class StoragePathService
    {
        private const string AppFolderName = "MECCG Deck Builder";
        private const string PortableFlagFileName = "portable.flag";

        private static readonly Lazy<bool> s_isPortable = new(() =>
        {
            try
            {
                string portableFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PortableFlagFileName);
                return File.Exists(portableFlag);
            }
            catch
            {
                return false;
            }
        });

        /// <summary>
        /// True if portable.flag is present in the application directory.
        /// </summary>
        public static bool IsPortableMode => s_isPortable.Value;

        public static string AppBaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Root storage directory: Local application directory in portable mode,
        /// or %LOCALAPPDATA%\MECCG Deck Builder\ in standard mode.
        /// </summary>
        public static string StorageRootDirectory
        {
            get
            {
                if (IsPortableMode)
                {
                    return AppBaseDirectory;
                }

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppFolderName);
            }
        }

        public static string GetDataDirectory()
        {
            string dir = IsPortableMode
                ? AppBaseDirectory
                : Path.Combine(StorageRootDirectory, "Data");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static string GetImagesDirectory()
        {
            string dir = IsPortableMode
                ? Path.Combine(AppBaseDirectory, "Images")
                : Path.Combine(StorageRootDirectory, "Images");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static string GetSetImageDirectory(string setCode)
        {
            string cleanSet = string.IsNullOrWhiteSpace(setCode) ? "UNKNOWN" : setCode.Trim();
            string dir = Path.Combine(GetImagesDirectory(), cleanSet);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        public static string GetCardImagePath(string setCode, string imageName)
        {
            return Path.Combine(GetSetImageDirectory(setCode), imageName ?? string.Empty);
        }

        public static string GetWritableDataFilePath(string fileName)
        {
            return Path.Combine(GetDataDirectory(), fileName);
        }

        public static string GetSettingsFilePath()
        {
            if (IsPortableMode)
            {
                return Path.Combine(AppBaseDirectory, "user_settings.json");
            }

            string userDir = StorageRootDirectory;
            if (!Directory.Exists(userDir))
            {
                Directory.CreateDirectory(userDir);
            }
            return Path.Combine(userDir, "user_settings.json");
        }

        /// <summary>
        /// Resolves the path to read an existing data file.
        /// Checks user LocalAppData Data directory first, then falls back to application BaseDirectory.
        /// </summary>
        public static string ResolveDataFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

            // 1. Check user data folder
            if (!IsPortableMode)
            {
                string userDataPath = Path.Combine(StorageRootDirectory, "Data", fileName);
                if (File.Exists(userDataPath))
                {
                    return userDataPath;
                }
            }

            // 2. Fall back to bundled application directory
            string appDirPath = Path.Combine(AppBaseDirectory, fileName);
            if (File.Exists(appDirPath))
            {
                return appDirPath;
            }

            // 3. Fall back to writable path for creation
            return GetWritableDataFilePath(fileName);
        }

        /// <summary>
        /// Resolves the path to read an existing card image.
        /// Checks user image cache first, then checks legacy/bundled set directories in application BaseDirectory.
        /// </summary>
        public static string ResolveImageReadPath(string setCode, string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName)) return string.Empty;

            string cleanSet = string.IsNullOrWhiteSpace(setCode) ? "UNKNOWN" : setCode.Trim();

            // 1. Check user image cache (%LOCALAPPDATA%/MECCG Deck Builder/Images/{Set}/{imageName})
            string cachePath = GetCardImagePath(cleanSet, imageName);
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            // 2. Check bundled or legacy local paths (e.g. BaseDirectory/{Set}/{imageName} or BaseDirectory/Images/{Set}/{imageName})
            string bundledLegacyPath = Path.Combine(AppBaseDirectory, cleanSet, imageName);
            if (File.Exists(bundledLegacyPath))
            {
                return bundledLegacyPath;
            }

            string bundledImagesPath = Path.Combine(AppBaseDirectory, "Images", cleanSet, imageName);
            if (File.Exists(bundledImagesPath))
            {
                return bundledImagesPath;
            }

            return cachePath;
        }
    }
}