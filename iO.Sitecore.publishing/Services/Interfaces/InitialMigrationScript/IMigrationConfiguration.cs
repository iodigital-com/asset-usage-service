using System;

namespace iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript
{
    public interface IMigrationConfiguration
    {
        string DatabaseName { get; }
        string RootItemId { get; }
        string ContentHubEndpoint { get; }
        int MaxConcurrency { get; }
    }
}
