using Sitecore.Data;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Services.Core.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace iO.Sitecore.publishing.Events
{
    public class InitialItemAssetlink
    {
        private readonly Database _masterDatabase;

        public InitialItemAssetlink()
        {
            _masterDatabase = Factory.GetDatabase("master");
            GetAllItemsFromWebDatabase();
        }

        public void GetAllItemsFromWebDatabase()
        {
            Log.Info($"{_masterDatabase}", this);
            
            var rootItem = _masterDatabase.GetRootItem();
         
            if (rootItem == null)
            {
                Log.Warn("Root item not found in master database", this);
                return;
            }
      
            var itemsToDictionary = rootItem.Axes.GetDescendants().ToDictionary(item => item.ID.ToString(), item => (object)item);
    
            Log.Info($"Retrieved {itemsToDictionary.Count} items from master database", this);
        }
    }
}
