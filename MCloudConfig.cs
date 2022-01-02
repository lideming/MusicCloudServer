using System;
using System.Collections.Generic;
using System.IO;

namespace MCloudServer
{

    // The configration will be read from appsettings.json
    public class MCloudConfig
    {
        public DbType DbType { get; set; }
        // "dbtype": "pg" | "sqlite"
        // Thanks to EF Core, it can easily support two or more database engines.

        public string DbStr { get; set; }
        // "dbstr"
        // for SQLite, it's only the filename (i.e., without "Filename=").
        // for PostgreSQL, it's the connection string.

        public string StaticDir { get; set; }
        // "staticdir"
        // if exists, the server will serve static files

        public string StorageDir { get; set; }
        // "storagedir"
        // the directory to store track files

        public string StorageUrlBase { get; set; }

        public string Passcode { get; set; }
        // "passcode"

        public List<Converter> Converters { get; set; } = new List<Converter>();

        public bool ConverterDebug { get; set; }

        public bool NotesEnabled { get; set; } = true;

        public bool DiscussionEnabled { get; set; } = true;

        public bool TrackCommentsEnabled { get; set; } = true;

        public bool TryResolveStoragePath(string prefixedPath, out string fsPath)
        {
            if (TryGetStoragePath(prefixedPath, out fsPath))
            {
                fsPath = Path.Combine(StorageDir, fsPath);
                return true;
            }
            return false;
        }

        public bool TryGetStoragePath(string prefixedPath, out string relPath)
        {
            if (prefixedPath.StartsWith("storage/"))
            {
                relPath = prefixedPath.Substring("storage/".Length);
                return true;
            }
            relPath = null;
            return false;
        }

        public string ResolveStoragePath(string storagePath)
        {
            if (!TryResolveStoragePath(storagePath, out var r))
                throw new Exception($"Failed to resolve storage path '{storagePath}'");
            return r;
        }

        public string GetStoragePath(string storagePath)
        {
            if (!TryGetStoragePath(storagePath, out var r))
                throw new Exception($"Failed to get storage path '{storagePath}'");
            return r;
        }

        public Converter FindConverter(string name) => Converters.Find(x => x.Name == name);

        public class Converter
        {
            public string Name { get; set; }
            public string Format { get; set; }
            public int Bitrate { get; set; }
            public string CommandLine { get; set; }
            public string Type { get; set; }

            public string GetCommandLine(string inputFile, string outputFile)
                => string.Format(CommandLine, inputFile, outputFile);
        }
    }

    public enum DbType
    {
        SQLite = 0,
        PostgreSQL = 1,
        Pg = 1,
    }
}