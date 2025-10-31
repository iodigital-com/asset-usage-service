namespace iO.Sitecore.Publishing.Services
{
    public interface IAuditLoggingService
    {
        void WriteAudit(object record);
    }
}