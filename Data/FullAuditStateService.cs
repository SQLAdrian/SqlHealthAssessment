using System.Collections.Concurrent;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Data
{
    /// <summary>
    /// Stores state for the Full Audit page to persist across navigation
    /// </summary>
    public class FullAuditStateService
    {
        private string? _selectedConnectionId;
        private string? _selectedServer;
        private readonly ConcurrentDictionary<string, ScriptExecutionResult> _executionResults = new();
        private bool _isRunning;
        private bool _isTesting;
        private readonly object _lock = new();
        private DateTime _lastClearTime = DateTime.Now;

        public string? SelectedConnectionId
        {
            get => _selectedConnectionId;
            set => _selectedConnectionId = value;
        }

        public string? SelectedServer
        {
            get => _selectedServer;
            set => _selectedServer = value;
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => _isRunning = value;
        }

        public bool IsTesting
        {
            get => _isTesting;
            set => _isTesting = value;
        }

        public void AddExecutionResult(ScriptExecutionResult result)
        {
            // Clear large result data to save memory - we only need metadata
            if (result.Results != null && result.Results.Count > 0)
            {
                // Keep only summary info, clear the actual data to free memory
                result.RowsAffected = result.Results.Count;
                result.Results = null; // Free the memory
            }
            _executionResults[result.ScriptName] = result;
        }

        public void RemoveExecutionResult(string scriptName)
        {
            _executionResults.TryRemove(scriptName, out _);
        }

        public void ClearExecutionResults()
        {
            _executionResults.Clear();
            _lastClearTime = DateTime.Now;
            // GC will reclaim memory naturally - no forced collection needed
        }

        public void ClearAndAddResults(IEnumerable<ScriptExecutionResult> results)
        {
            _executionResults.Clear();
            foreach (var result in results)
            {
                AddExecutionResult(result);
            }
            _lastClearTime = DateTime.Now;
        }

        public ScriptExecutionResult? GetExecutionResult(string scriptName)
        {
            return _executionResults.TryGetValue(scriptName, out var result) ? result : null;
        }

        public List<ScriptExecutionResult> GetAllExecutionResults()
        {
            return _executionResults.Values.ToList();
        }

        public bool HasExecutionResults => !_executionResults.IsEmpty;

        public int ResultCount => _executionResults.Count;

        /// <summary>
        /// Clears all execution results and releases references so the GC can reclaim memory naturally.
        /// Forced GC.Collect was removed as it causes UI freezes and promotes objects to Gen2 prematurely.
        /// </summary>
        public void ForceCleanup()
        {
            ClearExecutionResults();
            _lastClearTime = DateTime.Now;
        }
    }
}
