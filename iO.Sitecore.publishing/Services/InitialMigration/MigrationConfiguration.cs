using iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript;
using Sitecore.Configuration;

namespace iO.Sitecore.Publishing.Configuration
{
    public class MigrationConfiguration : IMigrationConfiguration
    {
        private const string DatabaseNameSetting = "AssetUsageService.DatabaseName";
        private const string RootItemIdSetting = "AssetUsageService.RootItemId";
        private const string ContentHubEndpointSetting = "AssetUsageService.ContentHubEndpoint";
        private const string MaxConcurrencySetting = "AssetUsageService.MaxConcurrency";

        private const string DefaultDatabaseName = "master";
        private const string DefaultMaxConcurrency = "100";
        private const int MinConcurrency = 1;
        private const int MaxConcurrencyLimit = 200;

        public string DatabaseName { get; }
        public string RootItemId { get; }
        public string ContentHubEndpoint { get; }
        public int MaxConcurrency { get; }

        public MigrationConfiguration()
        {
            DatabaseName = Settings.GetSetting(DatabaseNameSetting, DefaultDatabaseName);
            RootItemId = Settings.GetSetting(RootItemIdSetting);
            ContentHubEndpoint = Settings.GetSetting(ContentHubEndpointSetting);
            MaxConcurrency = ParseConcurrency(Settings.GetSetting(MaxConcurrencySetting, DefaultMaxConcurrency));
        }

        private static int ParseConcurrency(string setting)
        {
            if (int.TryParse(setting, out int value) && value >= MinConcurrency && value <= MaxConcurrencyLimit)
            {
                return value;
            }
            return int.Parse(DefaultMaxConcurrency);
        }
    }
}