using System;
using System.Collections.Generic;


namespace iO.Sitecore.Publishing.Models
{
    public class AssetUsageEvent
    {
            public List<string> PublicLink { get; set; }
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
