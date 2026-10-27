using System;
using System.Threading;
using System.Threading.Tasks;
using Fulcrum.Simulation;
using UnityEditor;
using UnityEngine.UIElements;

namespace Fulcrum.Editor.Windows
{
    /// <summary>
    /// Runs the analysis off the main thread and reports back on it.
    /// </summary>
    /// <remarks>
    /// Two things make interactive editing possible. Requests are debounced, so dragging a
    /// slider queues one run rather than one per frame. And the run happens on a worker
    /// thread — the simulators are plain C# precisely so that this is allowed — leaving the
    /// editor free to keep drawing.
    ///
    /// A superseded run is cancelled rather than awaited. The result of the parameter value
    /// from two frames ago is not worth the wait, and applying it after a newer one would
    /// show the wrong numbers.
    /// </remarks>
    public sealed class SimulationDriver : IDisposable
    {
        private readonly BalanceAnalyzer _analyzer = new BalanceAnalyzer();
        private readonly VisualElement _scheduler;
        private IVisualElementScheduledItem _pending;
        private CancellationTokenSource _cancellation;
        private BalanceSnapshot _queuedBalance;
        private TrackBinding _queuedBinding;
        private SimulationRun _queuedRun;
        private bool _isDisposed;

        /// <summary>Milliseconds of quiet before a queued run starts.</summary>
        public long DebounceMs { get; set; } = 150L;

        /// <summary>Raised on the main thread when a run finishes.</summary>
        public event Action<BalanceAnalysis> Completed;

        /// <summary>Raised on the main thread when a run fails.</summary>
        public event Action<Exception> Failed;

        /// <summary>Whether a run is in flight.</summary>
        public bool IsRunning { get; private set; }

        public SimulationDriver(VisualElement scheduler) => _scheduler = scheduler;

        /// <summary>
        /// Queues an analysis, replacing anything already queued or running.
        /// </summary>
        public void Request(BalanceSnapshot balance, TrackBinding binding, SimulationRun run)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SimulationDriver));

            // Revoke the previous result immediately, including the debounce interval.
            Cancel();
            _queuedBalance = balance;
            _queuedBinding = binding;
            _queuedRun = run;
            _pending = _scheduler.schedule.Execute(Start).StartingIn(DebounceMs);
        }

        /// <summary>Abandons anything queued or running.</summary>
        public void Cancel()
        {
            _pending?.Pause();
            _pending = null;
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _queuedBalance = null;
            _queuedBinding = null;
            IsRunning = false;
        }

        private void Start()
        {
            _pending = null;
            if (_isDisposed) return;

            var balance = _queuedBalance;
            var binding = _queuedBinding;
            var run = _queuedRun;
            if (balance == null || binding == null) return;

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            IsRunning = true;

            Task.Run(() =>
            {
                try
                {
                    var analysis = _analyzer.Analyze(balance, binding, run);
                    if (token.IsCancellationRequested) return;

                    // Results are handed back through delayCall because everything that
                    // consumes them touches the editor, which is main-thread only.
                    EditorApplication.delayCall += () =>
                    {
                        if (token.IsCancellationRequested) return;
                        IsRunning = false;
                        Completed?.Invoke(analysis);
                    };
                }
                catch (Exception exception)
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (token.IsCancellationRequested) return;
                        IsRunning = false;
                        Failed?.Invoke(exception);
                    };
                }
            }, token);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Cancel();
            Completed = null;
            Failed = null;
        }
    }
}
