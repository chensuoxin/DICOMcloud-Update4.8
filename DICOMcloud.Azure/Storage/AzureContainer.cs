using Azure.Core;
using Azure.Storage.Blobs;
using DICOMcloud.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DICOMcloud.Azure.IO
{
    public class AzureContainer : IStorageContainer
    {
        private readonly BlobContainerClient _containerClient;

        public AzureContainer(BlobContainerClient containerClient)
        {
            _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
            Name = containerClient.Name;
        }

        public string Name { get; }
        public string Connection { get { return _containerClient.Uri.ToString(); } }

        // 同步方法
        public void Delete()
        {
            _containerClient.DeleteIfExists();
        }

        public IStorageLocation GetLocation(string key, IMediaId mediaId = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            // 规范化路径
            key = key.Replace(Path.DirectorySeparatorChar, '/');
            var blobClient = _containerClient.GetBlobClient(key);

            return new AzureLocation(blobClient, mediaId);
        }

        public IEnumerable<IStorageLocation> GetLocations(string searchPattern = null)
        {
            var locations = new List<IStorageLocation>();

            try
            {
                var blobItems = _containerClient.GetBlobs(prefix: searchPattern);

                foreach (var blobItem in blobItems)
                {
                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    locations.Add(new AzureLocation(blobClient));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取位置列表时出错: {ex.Message}");
                throw;
            }

            return locations;
        }

        public bool LocationExists(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            key = key.Replace(Path.DirectorySeparatorChar, '/');
            var blobClient = _containerClient.GetBlobClient(key);
            return blobClient.Exists();
        }

        // 异步方法
        public async Task DeleteAsync()
        {
            await _containerClient.DeleteIfExistsAsync();
        }

        public async Task<IStorageLocation> GetLocationAsync(string key, IMediaId mediaId = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            key = key.Replace(Path.DirectorySeparatorChar, '/');
            var blobClient = _containerClient.GetBlobClient(key);

            return new AzureLocation(blobClient, mediaId);
        }

        public async Task<bool> LocationExistsAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            key = key.Replace(Path.DirectorySeparatorChar, '/');
            var blobClient = _containerClient.GetBlobClient(key);
            return await blobClient.ExistsAsync();
        }
    }
}