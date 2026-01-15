using System;

namespace iO.Sitecore.Publishing.Services
{
    public static class MigrationProgressTracker
    {
        public static bool IsRunning { get; set; }
        public static int TotalItems { get; set; }
        public static int ProcessedItems { get; set; }
        public static int SuccessCount { get; set; }
        public static int FailureCount { get; set; }
        public static string CurrentItem { get; set; }
        public static string ErrorMessage { get; set; }
        public static DateTime? StartTime { get; set; }
        public static DateTime? EndTime { get; set; }
        public static int CurrentPhase { get; set; }
        public static string PhaseDescription { get; set; }
        public static int ExtractedCount { get; set; }

        public static int ProgressPercentage
        {
            get
            {
                if (TotalItems == 0) return 0;
                return (int)((double)ProcessedItems / TotalItems * 100);
            }
        }

        public static void Reset()
        {
            IsRunning = false;
            TotalItems = 0;
            ProcessedItems = 0;
            SuccessCount = 0;
            FailureCount = 0;
            CurrentItem = null;
            ErrorMessage = null;
            StartTime = null;
            EndTime = null;
            CurrentPhase = 0;
            PhaseDescription = null;
            ExtractedCount = 0;
        }
    }
}