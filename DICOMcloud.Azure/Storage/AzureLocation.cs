using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DICOMcloud.IO;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DICOMcloud.Azure.IO
{
    public class AzureLocation : IStorageLocation
    {
        private readonly BlobClient _blobClient;
        private readonly IMediaId _mediaId;
        private long? _size;
        private BlobProperties _cachedProperties;

        public AzureLocation(BlobClient blobClient, IMediaId mediaId = null)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            _mediaId = mediaId;
            Name = Path.GetFileName(blobClient.Name);
            ID = blobClient.Uri.ToString();
        }

        #region 接口属性实现
        public string Name { get; }
        public string ID { get; }
        public IMediaId MediaId { get { return _mediaId; } }

        public string ContentType
        {
            get
            {
                EnsureProperties();
                return _cachedProperties?.ContentType;
            }
        }

        public string Metadata
        {
            get
            {
                EnsureProperties();
                if (_cachedProperties?.Metadata != null &&
                    _cachedProperties.Metadata.TryGetValue("meta", out var metaValue))
                {
                    return metaValue;
                }
                return null;
            }
            set
            {
                if (Exists())
                {
                    var properties = _blobClient.GetProperties();
                    var metadata = properties.Value.Metadata;

                    if (string.IsNullOrEmpty(value))
                    {
                        metadata.Remove("meta");
                    }
                    else
                    {
                        metadata["meta"] = value;
                    }

                    _blobClient.SetMetadata(metadata);
                    _cachedProperties = null; // 清除缓存
                }
            }
        }
        #endregion

        #region 接口方法实现
        public bool Exists()
        {
            return _blobClient.Exists();
        }

        public long GetSize()
        {
            if (_size.HasValue)
            {
                return _size.Value;
            }

            EnsureProperties();
            _size = _cachedProperties?.ContentLength ?? 0;
            return _size.Value;
        }

        public void Delete()
        {
            _blobClient.DeleteIfExists();
            _cachedProperties = null;
            _size = null;
        }

        public Stream Download()
        {
            try
            {
                var response = _blobClient.Download();
                return response.Value.Content;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new FileNotFoundException($"Blob not found: {_blobClient.Name}", ex);
            }
        }

        public void Download(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            try
            {
                _blobClient.DownloadTo(stream);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new FileNotFoundException($"Blob not found: {_blobClient.Name}", ex);
            }
        }

        public void Upload(Stream stream, string contentType = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType ?? GetDefaultContentType()
                }
            };

            _blobClient.Upload(stream, options);
            _cachedProperties = null;
            _size = null;
        }

        public void Upload(byte[] buffer, string contentType = null)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType ?? GetDefaultContentType()
                }
            };

            using (var stream = new MemoryStream(buffer))
            {
                _blobClient.Upload(stream, options);
            }

            _cachedProperties = null;
            _size = null;
        }

        public void Upload(string filename, string contentType = null)
        {
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentException("Filename cannot be null or empty", nameof(filename));

            if (!File.Exists(filename))
                throw new FileNotFoundException($"File not found: {filename}");

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType ?? GetDefaultContentType()
                }
            };

            _blobClient.Upload(filename, options);
            _cachedProperties = null;
            _size = null;
        }

        public Stream GetReadStream()
        {
            return Download();
        }
        #endregion

        #region 私有辅助方法
        private void EnsureProperties()
        {
            if (_cachedProperties == null && Exists())
            {
                try
                {
                    var response = _blobClient.GetProperties();
                    _cachedProperties = response.Value;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Blob不存在，保持_cachedProperties为null
                }
            }
        }

        private string GetDefaultContentType()
        {
            // 根据文件扩展名返回默认的ContentType
            var extension = Path.GetExtension(Name).ToLowerInvariant();

            // 使用C# 7.3兼容的switch语句替换switch表达式
            switch (extension)
            {
                case ".dcm":
                    return "application/dicom";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                case ".tiff":
                case ".tif":
                    return "image/tiff";
                case ".pdf":
                    return "application/pdf";
                case ".txt":
                    return "text/plain";
                case ".xml":
                    return "application/xml";
                case ".json":
                    return "application/json";
                case ".zip":
                    return "application/zip";
                default:
                    return "application/octet-stream";
            }
        }
        #endregion

        #region 异步方法（额外功能）
        public async Task<bool> ExistsAsync()
        {
            return await _blobClient.ExistsAsync();
        }

        public async Task DeleteAsync()
        {
            await _blobClient.DeleteIfExistsAsync();
            _cachedProperties = null;
            _size = null;
        }

        public async Task<Stream> DownloadAsync()
        {
            try
            {
                var response = await _blobClient.DownloadAsync();
                return response.Value.Content;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new FileNotFoundException($"Blob not found: {_blobClient.Name}", ex);
            }
        }

        public async Task UploadAsync(Stream stream, string contentType = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType ?? GetDefaultContentType()
                }
            };

            await _blobClient.UploadAsync(stream, options);
            _cachedProperties = null;
            _size = null;
        }
        #endregion
    }
}