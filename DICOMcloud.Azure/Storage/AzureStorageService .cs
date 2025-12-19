using Azure.Identity;
using Azure.Storage.Blobs;
using DICOMcloud.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DICOMcloud.Azure.IO
{
    public class AzureStorageService : MediaStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IKeyProvider _keyProvider;

        public AzureStorageService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("连接字符串不能为空", nameof(connectionString));

            _blobServiceClient = new BlobServiceClient(connectionString);
            _keyProvider = new AzureKeyProvider();
        }

        public AzureStorageService(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _keyProvider = new AzureKeyProvider();
        }

        public AzureStorageService(Uri blobServiceUri)
        {
            if (blobServiceUri == null)
                throw new ArgumentNullException(nameof(blobServiceUri));

            _blobServiceClient = new BlobServiceClient(blobServiceUri, new DefaultAzureCredential());
            _keyProvider = new AzureKeyProvider();
        }

        // 实现基类必需的方法
        protected override IStorageContainer GetContainer(string containerKey)
        {
            containerKey = GetValidContainerKey(containerKey);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerKey);

            // 创建容器（如果不存在）
            containerClient.CreateIfNotExists();

            return new AzureContainer(containerClient);
        }

        protected override IEnumerable<IStorageContainer> GetContainers(string containerKey)
        {
            containerKey = GetValidContainerKey(containerKey);
            var containers = new List<IStorageContainer>();

            try
            {
                // 使用同步方式列出容器
                var containerItems = _blobServiceClient.GetBlobContainers();

                foreach (var containerItem in containerItems)
                {
                    if (string.IsNullOrEmpty(containerKey) || containerItem.Name.StartsWith(containerKey))
                    {
                        var containerClient = _blobServiceClient.GetBlobContainerClient(containerItem.Name);
                        containers.Add(new AzureContainer(containerClient));
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录日志
                System.Diagnostics.Debug.WriteLine($"获取容器列表时出错: {ex.Message}");
                throw;
            }

            return containers;
        }

        protected override bool ContainerExists(string containerKey)
        {
            containerKey = GetValidContainerKey(containerKey);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerKey);
            return containerClient.Exists();
        }

        protected override IKeyProvider GetKeyProvider()
        {
            return _keyProvider;
        }

        // 容器名称验证和标准化
        private static string GetValidContainerKey(string containerKey)
        {
            if (string.IsNullOrEmpty(containerKey))
                return containerKey;

            // Azure容器名称必须小写
            containerKey = containerKey.ToLower();

            // 替换无效字符
            char[] invalidChars = "!@#$%^&*()+=[]{}\\|;':\",.<>/?~`".ToCharArray();
            foreach (char invalidChar in invalidChars)
            {
                containerKey = containerKey.Replace(invalidChar.ToString(), "-");
            }

            // 确保以字母或数字开头
            if (containerKey.Length > 0 && !char.IsLetterOrDigit(containerKey[0]))
            {
                containerKey = "c" + containerKey.Substring(1);
            }

            // 确保长度在3-63个字符之间
            if (containerKey.Length < 3)
                containerKey = containerKey.PadRight(3, '0');
            else if (containerKey.Length > 63)
                containerKey = containerKey.Substring(0, 63);

            return containerKey;
        }
    }
}