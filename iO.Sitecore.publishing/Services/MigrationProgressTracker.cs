using System;
using System.Threading;

namespace iO.Sitecore.Publishing.Services
{
    public static class MigrationProgressTracker
    {
        private static readonly object _lock = new object();
        private static bool _isRunning;
        private static int _totalItems;
        private static int _processedItems;
        private static int _successCount;
        private static int _failureCount;
        private static string _currentItem = string.Empty;
        private static string _errorMessage = string.Empty;
        private static DateTime? _startTime;
        private static DateTime? _endTime;

        public static bool IsRunning
        {
            get { lock (_lock) { return _isRunning; } }
            set { lock (_lock) { _isRunning = value; } }
        }

        public static int TotalItems
        {
            get { return Interlocked.CompareExchange(ref _totalItems, 0, 0); }
            set { Interlocked.Exchange(ref _totalItems, value); }
        }

        public static int ProcessedItems
        {
            get { return Interlocked.CompareExchange(ref _processedItems, 0, 0); }
            set { Interlocked.Exchange(ref _processedItems, value); }
        }

        public static int SuccessCount
        {
            get { return Interlocked.CompareExchange(ref _successCount, 0, 0); }
            set { Interlocked.Exchange(ref _successCount, value); }
        }

        public static int FailureCount
        {
            get { return Interlocked.CompareExchange(ref _failureCount, 0, 0); }
            set { Interlocked.Exchange(ref _failureCount, value); }
        }

        public static string CurrentItem
        {
            get { lock (_lock) { return _currentItem; } }
            set { lock (_lock) { _currentItem = value; } }
        }

        public static string ErrorMessage
        {
            get { lock (_lock) { return _errorMessage; } }
            set { lock (_lock) { _errorMessage = value; } }
        }

        public static DateTime? StartTime
        {
            get { lock (_lock) { return _startTime; } }
            set { lock (_lock) { _startTime = value; } }
        }

        public static DateTime? EndTime
        {
            get { lock (_lock) { return _endTime; } }
            set { lock (_lock) { _endTime = value; } }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _isRunning = false;
                Interlocked.Exchange(ref _totalItems, 0);
                Interlocked.Exchange(ref _processedItems, 0);
                Interlocked.Exchange(ref _successCount, 0);
                Interlocked.Exchange(ref _failureCount, 0);
                _currentItem = string.Empty;
                _errorMessage = string.Empty;
                _startTime = null;
                _endTime = null;
            }
        }

        public static int ProgressPercentage
        {
            get
            {
                int total = Interlocked.CompareExchange(ref _totalItems, 0, 0);
                int processed = Interlocked.CompareExchange(ref _processedItems, 0, 0);
                return total > 0 ? (int)((double)processed / total * 100) : 0;
            }
        }
    }
}