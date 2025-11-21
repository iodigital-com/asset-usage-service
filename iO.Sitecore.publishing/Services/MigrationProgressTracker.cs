using System;

namespace iO.Sitecore.Publishing.Services
{
    public static class MigrationProgressTracker
    {
        private static readonly object _lock = new object();
        
        public static bool IsRunning { get; set; }
        public static int TotalItems { get; set; }
        public static int ProcessedItems { get; set; }
        public static int SuccessCount { get; set; }
        public static int FailureCount { get; set; }
        public static string CurrentItem { get; set; }
        public static string ErrorMessage { get; set; }
        public static DateTime? StartTime { get; set; }
        public static DateTime? EndTime { get; set; }

        public static void Reset()
        {
            lock (_lock)
            {
                IsRunning = false;
                TotalItems = 0;
                ProcessedItems = 0;
                SuccessCount = 0;
                FailureCount = 0;
                CurrentItem = string.Empty;
                ErrorMessage = string.Empty;
                StartTime = null;
                EndTime = null;
            }
        }

        public static int ProgressPercentage
        {
            get
            {
                lock (_lock)
                {
                    // Read values atomically to avoid race conditions
                    var total = TotalItems;
                    var processed = ProcessedItems;
                    return total > 0 ? (int)((double)processed / total * 100) : 0;
                }
            }
        }
    }
}