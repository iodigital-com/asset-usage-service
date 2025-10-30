using Sitecore.Data;
using System;

namespace iO.Sitecore.publishing.Models
{
    public class ItemUpdateInfo
    {
        public ID ItemId { get; set; }
        public string ItemPath { get; set; }
        public string Language { get; set; }
        public int Version { get; set; }
        public ID OldRevisionId { get; set; }
        public ID NewRevisionId { get; set; }
        public DateTime OldUpdated { get; set; }
        public DateTime NewUpdated { get; set; }
        public string Action { get; set; }
        public string Result { get; set; }
        public string PublishMode { get; set; }
        public string TargetDatabaseName { get; set; }
        public string SourceDatabaseName { get; set; }
    }
}
