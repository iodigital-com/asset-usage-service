using System;
using System.IO;
using System.Text.Json;

namespace iO.Sitecore.Publishing.Services
{
    public sealed class AuditLoggingService : IAuditLoggingService
    {
        private readonly string auditLogPath;
        private readonly PublishLoggingService loggingService;
        private static readonly object FileLock = new object();

        public AuditLoggingService(string auditLogPath, PublishLoggingService loggingService)
        {
            this.auditLogPath = string.IsNullOrWhiteSpace(auditLogPath) ? throw new ArgumentException(nameof(auditLogPath)) : auditLogPath;
            this.loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        }

        public void WriteAudit(object record)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                };

                var json = JsonSerializer.Serialize(record, options);

                var directory = Path.GetDirectoryName(auditLogPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (FileLock)
                {
                    File.AppendAllText(auditLogPath, json + Environment.NewLine);
                }

                loggingService.LogAuditWriteComplete();
            }
            catch (Exception exception)
            {
                loggingService.LogWriteAuditError(exception);
            }
        }
    }
}