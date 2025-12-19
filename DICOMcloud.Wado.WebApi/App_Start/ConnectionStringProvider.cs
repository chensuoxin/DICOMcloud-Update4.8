using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DICOMcloud.DataAccess.Database;
using System.Configuration; // 添加这个命名空间引用

namespace DICOMcloud.Wado
{
    public class ConnectionStringProvider : IConnectionStringProvider
    {
        public string ConnectionString => ConfigurationManager.AppSettings["app:PacsDataArchieve"];
    }
}