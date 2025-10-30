using Sitecore.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iO.Sitecore.publishing.Models
{
    public class ItemProcessingInfo
    {
        public ID ItemId { get; set; }
        public string ItemPath { get; set; }
        public string Language { get; set; }
        public int Version { get; set; }
        public ID SourceRevisionId { get; set; }
        public DateTime SourceUpdated { get; set; }
        public ID TargetRevisionId { get; set; }
        public DateTime TargetUpdated { get; set; }
        public string Action { get; set; }
        public DateTime ProcessingTime { get; set; }
        public bool HasVersionInfo { get; set; }
    }
}
