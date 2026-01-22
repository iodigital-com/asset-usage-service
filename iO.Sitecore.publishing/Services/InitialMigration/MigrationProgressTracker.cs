using System;
using System.Threading;

namespace iO.Sitecore.Publishing.Services
{
    public static class MigrationProgressTracker
    {
        private const int BooleanTrue = 1;
        private const int BooleanFalse = 0;
        private const long NoDateTime = 0L;

        private static int _isRunning;
        private static int _totalItems;
        private static int _processedItems;
        private static int _successCount;
        private static int _failureCount;
        private static string _currentItem;
        private static string _errorMessage;
        private static long _startTimeTicks;
        private static long _endTimeTicks;
        private static int _currentPhase;
        private static string _phaseDescription;
        private static int _extractedCount;

        public static bool IsRunning
        {
            get => Interlocked.CompareExchange(ref _isRunning, BooleanFalse, BooleanFalse) == BooleanTrue;
            set => Interlocked.Exchange(ref _isRunning, value ? BooleanTrue : BooleanFalse);
        }

        public static int TotalItems
        {
            get => Interlocked.CompareExchange(ref _totalItems, 0, 0);
            set => Interlocked.Exchange(ref _totalItems, value);
        }

        public static int ProcessedItems
        {
            get => Interlocked.CompareExchange(ref _processedItems, 0, 0);
            set => Interlocked.Exchange(ref _processedItems, value);
        }

        public static int SuccessCount
        {
            get => Interlocked.CompareExchange(ref _successCount, 0, 0);
            set => Interlocked.Exchange(ref _successCount, value);
        }

        public static int FailureCount
        {
            get => Interlocked.CompareExchange(ref _failureCount, 0, 0);
            set => Interlocked.Exchange(ref _failureCount, value);
        }

        public static string CurrentItem
        {
            get => Interlocked.CompareExchange(ref _currentItem, null, null);
            set => Interlocked.Exchange(ref _currentItem, value);
        }

        public static string ErrorMessage
        {
            get => Interlocked.CompareExchange(ref _errorMessage, null, null);
            set => Interlocked.Exchange(ref _errorMessage, value);
        }

        public static DateTime? StartTime
        {
            get
            {
                long ticks = Interlocked.Read(ref _startTimeTicks);
                return ticks == NoDateTime ? (DateTime?)null : new DateTime(ticks, DateTimeKind.Utc);
            }
            set => Interlocked.Exchange(ref _startTimeTicks, value?.Ticks ?? NoDateTime);
        }

        public static DateTime? EndTime
        {
            get
            {
                long ticks = Interlocked.Read(ref _endTimeTicks);
                return ticks == NoDateTime ? (DateTime?)null : new DateTime(ticks, DateTimeKind.Utc);
            }
            set => Interlocked.Exchange(ref _endTimeTicks, value?.Ticks ?? NoDateTime);
        }

        public static int CurrentPhase
        {
            get => Interlocked.CompareExchange(ref _currentPhase, 0, 0);
            set => Interlocked.Exchange(ref _currentPhase, value);
        }

        public static string PhaseDescription
        {
            get => Interlocked.CompareExchange(ref _phaseDescription, null, null);
            set => Interlocked.Exchange(ref _phaseDescription, value);
        }

        public static int ExtractedCount
        {
            get => Interlocked.CompareExchange(ref _extractedCount, 0, 0);
            set => Interlocked.Exchange(ref _extractedCount, value);
        }

        public static int ProgressPercentage
        {
            get
            {
                int total = TotalItems;
                if (total == 0)
                {
                    return 0;
                }
                int processed = ProcessedItems;
                return (int)((double)processed / total * 100);
            }
        }

        public static void IncrementProcessedItems()
        {
            Interlocked.Increment(ref _processedItems);
        }

        public static void IncrementSuccessCount()
        {
            Interlocked.Increment(ref _successCount);
        }

        public static void IncrementFailureCount()
        {
            Interlocked.Increment(ref _failureCount);
        }

        public static void IncrementExtractedCount()
        {
            Interlocked.Increment(ref _extractedCount);
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