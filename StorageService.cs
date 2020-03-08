using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using COSXML;
using COSXML.Auth;

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

    public class QcloudStorageService : StorageService
    {
        public override StorageMode Mode => StorageMode.PutUrl;

        CosXml cos;
        CosXmlConfig cosConfig;
        string bucket;

        public QcloudStorageService(string[] args)
        {
            // args: "qcloud" | bucket | region | secret_id | secret_key
            bucket = args[1];
            cosConfig = new CosXmlConfig.Builder()
                .IsHttps(true)
                .SetAppid(args[1].Split('-').Last())
                .SetRegion(args[2])
                .Build();
            var cred = new DefaultQCloudCredentialProvider(args[3], args[4], 120);

            cos = new CosXmlServer(cosConfig, cred);
        }

        public override Task<StorageUploadParameters> RequestUpload(RequestUploadOptions options)
        {
            var headers = new Dictionary<string, string>();
            if (options.Length > 0)
            {
                headers.Add("Content-Length", options.Length.ToString());
            }

            var url = cos.GenerateSignURL(new COSXML.Model.Tag.PreSignatureStruct
            {
                isHttps = true,
                httpMethod = "PUT",
                appid = cosConfig.Appid,
                bucket = this.bucket,
                region = cosConfig.Region,
                headers = headers,
                key = options.DestFilePath,
            });
            return Task.FromResult(new StorageUploadParameters
            {
                Url = url,
                Method = "PUT"
            });
        }

        public override async Task GetFile(string url, string destFilePath)
        {
            var stream = await new HttpClient().GetStreamAsync(url.Substring(0, url.IndexOf("?")));
            using (var fs = File.Open(destFilePath, FileMode.Create))
            {
                await stream.CopyToAsync(fs);
            }
        }

        public override Task DeleteFile(string filepath)
        {
            var url = cos.GenerateSignURL(new COSXML.Model.Tag.PreSignatureStruct
            {
                isHttps = true,
                httpMethod = "DELETE",
                appid = cosConfig.Appid,
                bucket = this.bucket,
                region = cosConfig.Region,
                key = filepath,
            });
            return new HttpClient().DeleteAsync(url);
        }
    }
}