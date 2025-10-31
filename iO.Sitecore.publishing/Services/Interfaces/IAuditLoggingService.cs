using System;
using System.Collections.Generic;

namespace iO.Sitecore.Publishing.Services
{
    public interface IAuditLoggingService
    {
        void WriteAudit(object record);
    }
}