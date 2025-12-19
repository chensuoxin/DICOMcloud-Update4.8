using Azure; // 提供 RequestFailedException
using Azure.Identity; // 提供 DefaultAzureCredential
using Azure.Storage.Queues;
using DICOMcloud.Messaging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace DICOMcloud.Azure.Messaging
{
    public class AzureMessageSender : IMessageSender
    {
        private readonly QueueServiceClient _queueServiceClient;

        public AzureMessageSender(QueueServiceClient queueServiceClient)
        {
            _queueServiceClient = queueServiceClient ?? throw new ArgumentNullException(nameof(queueServiceClient));
        }

        // 构造函数：使用连接字符串
        public AzureMessageSender(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("连接字符串不能为空", nameof(connectionString));

            _queueServiceClient = new QueueServiceClient(connectionString);
        }

        // 构造函数：使用无密码身份验证（推荐用于生产环境）
        public AzureMessageSender(Uri queueServiceUri)
        {
            if (queueServiceUri == null)
                throw new ArgumentNullException(nameof(queueServiceUri));

            // 使用正确的构造函数：QueueServiceClient 接受 Uri 和 TokenCredential
            _queueServiceClient = new QueueServiceClient(queueServiceUri, new DefaultAzureCredential());
        }

        public void SendMessage(ITransportMessage message, TimeSpan? delay = null)
        {
            // 同步版本调用异步版本
            SendMessageAsync(message, delay).GetAwaiter().GetResult();
        }

        public async Task SendMessageAsync(ITransportMessage message, TimeSpan? delay = null)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                // 获取队列客户端
                QueueClient queueClient = _queueServiceClient.GetQueueClient(message.Name);

                // 创建队列（如果不存在）
                await queueClient.CreateIfNotExistsAsync();

                // 序列化消息
                string messageText = JsonConvert.SerializeObject(message);

                // 发送消息（visibilityTimeout 对应旧的 delay 参数）
                // visibilityTimeout: 消息在变得可见之前的延迟时间
                await queueClient.SendMessageAsync(
                    messageText: messageText,
                    visibilityTimeout: delay,
                    timeToLive: null); // timeToLive 设置为 null 使用默认值
            }
            catch (RequestFailedException ex) // 使用正确的异常类型
            {
                // 处理Azure存储异常[2,3](@ref)
                System.Diagnostics.Debug.WriteLine($"发送消息到队列 {message.Name} 时出错: {ex.Message} (状态码: {ex.Status})");

                // 根据错误代码进行特定处理[1](@ref)
                if (ex.ErrorCode == "QueueNotFound")
                {
                    throw new InvalidOperationException($"队列不存在: {message.Name}", ex);
                }
                else if (ex.Status == 403) // 权限拒绝
                {
                    throw new UnauthorizedAccessException($"没有权限访问队列: {ex.Message}", ex);
                }

                throw new InvalidOperationException($"无法发送消息到队列: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送消息时发生意外错误: {ex.Message}");
                throw;
            }
        }

        #region 辅助方法和属性
        public QueueServiceClient QueueServiceClient
        {
            get { return _queueServiceClient; }
        }

        // 获取队列URL（用于调试或日志记录）
        public Uri GetQueueUri(string queueName)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            return queueClient.Uri;
        }

        // 检查队列是否存在[4](@ref)
        public async Task<bool> QueueExistsAsync(string queueName)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            var response = await queueClient.ExistsAsync();
            return response.Value;
        }

        // 获取队列近似消息计数
        public async Task<int> GetMessageCountAsync(string queueName)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            var properties = await queueClient.GetPropertiesAsync();
            return properties.Value.ApproximateMessagesCount;
        }

        // 删除队列（如果存在）[5](@ref)
        public async Task<bool> DeleteQueueIfExistsAsync(string queueName)
        {
            var queueClient = _queueServiceClient.GetQueueClient(queueName);
            var response = await queueClient.DeleteIfExistsAsync();
            return response.Value;
        }
        #endregion
    }
}