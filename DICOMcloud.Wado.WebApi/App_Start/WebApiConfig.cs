using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Configuration; // 使用标准的 ConfigurationManager

namespace DICOMcloud.Wado
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // 使用 ConfigurationManager 替代 CloudConfigurationManager
            string enabled = ConfigurationManager.AppSettings["cors:enabled"];

            if (bool.TryParse(enabled, out bool isEnabled) && isEnabled)
            {
                string origins = ConfigurationManager.AppSettings["cors:origins"];
                string headers = ConfigurationManager.AppSettings["cors:headers"];
                string methods = ConfigurationManager.AppSettings["cors:methods"];

                // 添加 CORS 消息处理器（如果 PreflightRequestsHandler 存在）
                if (!string.IsNullOrEmpty(origins))
                {
                    config.MessageHandlers.Add(new PreflightRequestsHandler(origins, headers, methods));
                    config.EnableCors(new EnableCorsAttribute(origins, headers, methods));
                }
                else
                {
                    // 如果没有配置特定源，启用全局 CORS
                    config.EnableCors(new EnableCorsAttribute("*", "*", "*"));
                }
            }

            // Web API 路由配置
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new
                {
                    id = RouteParameter.Optional
                }
            );
        }
    }
}