using DICOMcloud.IO;
using System;
using System.IO;
using System.Linq;

namespace DICOMcloud.Azure.IO
{
    public class AzureKeyProvider : IKeyProvider
    {
        /// <summary>
        /// 从 IMediaId 生成存储路径
        /// 兼容不同版本的 IMediaId 接口
        /// </summary>
        public string GetStorageKey(IMediaId key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            try
            {
                // 方法1: 使用反射动态获取属性（兼容性方案）
                return GetStorageKeyByReflection(key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"使用反射获取存储键失败: {ex.Message}");

                // 方法2: 使用备用方案
                return GetStorageKeyByBackup(key);
            }
        }

        /// <summary>
        /// 返回逻辑分隔符
        /// </summary>
        public string GetLogicalSeparator()
        {
            return "/";
        }

        /// <summary>
        /// 从完整存储路径中提取容器名称
        /// </summary>
        public string GetContainerName(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            key = key.Trim('/');
            var separator = GetLogicalSeparator();
            var firstSeparatorIndex = key.IndexOf(separator);

            if (firstSeparatorIndex >= 0)
            {
                return key.Substring(0, firstSeparatorIndex);
            }

            return key;
        }

        /// <summary>
        /// 从完整存储路径中提取位置名称
        /// </summary>
        public string GetLocationName(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            key = key.Trim('/');
            var separator = GetLogicalSeparator();
            var lastSeparatorIndex = key.LastIndexOf(separator);

            if (lastSeparatorIndex >= 0)
            {
                return key.Substring(lastSeparatorIndex + 1);
            }

            return key;
        }

        #region 私有辅助方法
        /// <summary>
        /// 使用反射动态获取 DICOM 属性（兼容性方案）
        /// </summary>
        private string GetStorageKeyByReflection(IMediaId key)
        {
            var parts = new System.Collections.Generic.List<string>();
            var keyType = key.GetType();

            // 尝试通过反射获取 DICOM 层次结构属性
            TryAddPropertyValue(key, keyType, "StudyInstanceUID", parts);
            TryAddPropertyValue(key, keyType, "SeriesInstanceUID", parts);
            TryAddPropertyValue(key, keyType, "SOPInstanceUID", parts);
            TryAddPropertyValue(key, keyType, "FrameNumber", parts);

            // 如果通过反射获取到了层次结构，使用它
            if (parts.Count > 0)
            {
                return string.Join(GetLogicalSeparator(), parts).ToLowerInvariant();
            }

            // 如果反射也失败了，使用备用方案
            return GetStorageKeyByBackup(key);
        }

        /// <summary>
        /// 尝试通过反射获取属性值并添加到列表
        /// </summary>
        private void TryAddPropertyValue(IMediaId key, Type keyType, string propertyName, System.Collections.Generic.List<string> parts)
        {
            try
            {
                var property = keyType.GetProperty(propertyName);
                if (property != null && property.CanRead)
                {
                    var value = property.GetValue(key);
                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        parts.Add(value.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"反射获取属性 {propertyName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 备用方案：使用其他可用属性或生成唯一标识
        /// </summary>
        private string GetStorageKeyByBackup(IMediaId key)
        {
            // 方案1: 尝试使用 ToString() 方法
            if (!string.IsNullOrEmpty(key.ToString()) && key.ToString() != key.GetType().FullName)
            {
                var keyString = key.ToString();
                // 清理字符串，确保适合作为路径
                return System.Text.RegularExpressions.Regex.Replace(keyString, @"[^a-zA-Z0-9\-\.]", "_")
                       .ToLowerInvariant();
            }

            // 方案2: 使用媒体ID的哈希值
            var hash = Math.Abs(key.GetHashCode()).ToString();
            return $"media_{hash}";
        }

        /// <summary>
        /// 辅助方法：从层次数组生成存储路径
        /// </summary>
        private string GetStorageKey(string[] hierarchy)
        {
            if (hierarchy == null || hierarchy.Length == 0)
                return Guid.NewGuid().ToString();

            return string.Join(GetLogicalSeparator(), hierarchy).ToLowerInvariant();
        }
        #endregion
    }
}