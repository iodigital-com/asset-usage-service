using iO.Sitecore.publishing.Services.Interfaces.InitialMigrationScript;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.SecurityModel;
using System;

namespace iO.Sitecore.Publishing.Services
{
    public class ItemCollector : IItemCollector
    {
        private readonly Database _database;

        public ItemCollector(string databaseName)
        {
            using (new SecurityDisabler())
            {
                _database = Factory.GetDatabase(databaseName);
            }

            if (_database == null)
            {
                throw new InvalidOperationException($"Database '{databaseName}' not found.");
            }
        }

        public Item[] CollectItems(string rootItemId)
        {
            if (string.IsNullOrWhiteSpace(rootItemId))
            {
                return Array.Empty<Item>();
            }

            using (new SecurityDisabler())
            {
                var rootItem = _database.GetItem(new ID(rootItemId));
                if (rootItem == null)
                {
                    return Array.Empty<Item>();
                }

                var descendants = rootItem.Axes.GetDescendants();
                var result = new Item[descendants.Length + 1];
                result[0] = rootItem;
                descendants.CopyTo(result, 1);

                return result;
            }
        }
    }
}