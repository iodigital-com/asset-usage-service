using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iO.Sitecore.publishing.Events
{
    public class AssetUsageEvent
    {
            public string ItemId { get; set; }
            public string ItemPath { get; set; }
            public string ItemName { get; set; }
            public string TemplateName { get; set; }
            public string Language { get; set; }
            public int Version { get; set; }
            public DateTime PublishedAtUtc { get; set; }
            public string PublishedBy { get; set; }
            public List<string> AssetIds { get; set; }
            public string TargetDatabase { get; set; }
    }
}
