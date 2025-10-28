using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetUsageService.Integration.ViewModel;

public class PublishedItemViewModel
{
    public string ItemId { get; set; }
    public List<string>? AssetIds { get; set; } = new List<string>();
    public string? PublicLink { get; set; }
}

