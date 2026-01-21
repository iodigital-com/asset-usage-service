using System;
using Sitecore.Data.Items;

namespace iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript
{
    public interface IItemCollector
    {
        Item[] CollectItems(string rootItemId);
    }
}
