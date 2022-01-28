using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class StorageService
    {
        public virtual StorageMode Mode { get; }

        public virtual Task<StorageUploadParameters> RequestUpload(RequestUploadOptions options)
        {
            throw new NotImplementedException();
        }

        public virtual Task GetFile(string url, string destFilePath)
        {
            throw new NotImplementedException();
        }

        public virtual async Task PutFile(RequestUploadOptions options, string srcFilePath)
        {
            var p = await RequestUpload(options);
            using (var fs = File.OpenRead(srcFilePath))
            {
                await new HttpClient().PutAsync(p.Url, new StreamContent(fs));
            }
        }

        public virtual Task DeleteFile(string filepath)
        {
            return Task.CompletedTask;
        }
    }

    public enum StorageMode
    {
        Direct,
        PutUrl
    }

    public class StorageUploadParameters
    {
        public string Url { get; set; }
        public string Method { get; set; }
    }

    public class RequestUploadOptions
    {
        public string DestFilePath { get; set; }
        public long Length { get; set; }
    }

    public class LocalStorageService : StorageService
    {
        public override StorageMode Mode => StorageMode.Direct;
    }
}