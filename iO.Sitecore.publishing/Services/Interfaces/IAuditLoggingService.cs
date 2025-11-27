namespace iO.Sitecore.Publishing.Interfaces.Services
{
    public interface IAuditLoggingService
    {
        void WriteAudit(object record);
    }
}