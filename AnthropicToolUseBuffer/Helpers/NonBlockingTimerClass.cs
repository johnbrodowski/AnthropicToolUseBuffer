using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AnthropicToolUseBuffer.Helpers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public class NonBlockingTimer : IDisposable
    {
        private readonly object _lockObject = new object();
        private volatile bool _isRunning = false;
        private volatile bool _isPaused = false;
        private volatile bool _isDisposed = false;
        private bool _repeat;
        private string _timerState = "stopped";
        private double _intervalSeconds;
        private double _elapsedTimeSeconds;
        private DateTime _startTime;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _timerTask;

        public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs args);

        public event AsyncEventHandler<EventArgs>? TimerStarted;

        public event AsyncEventHandler<EventArgs>? TimerStopped;

        public event AsyncEventHandler<EventArgs>? TimerCompleted;

        public event AsyncEventHandler<EventArgs>? TimerTick;

        public event AsyncEventHandler<EventArgs>? TimerPaused;

        public event AsyncEventHandler<StateChangedEventArgs>? StateChanged;

        public event AsyncEventHandler<ErrorEventArgs>? ErrorOccurred;

        #region Event Args Classes

        public class StateChangedEventArgs : EventArgs
        {
            public string Message { get; }
            public string RequestId { get; }

            public StateChangedEventArgs(string message, string requestId = "0000")
            {
                Message = message ?? throw new ArgumentNullException(nameof(message));
                RequestId = requestId ?? "0000";
            }
        }

        public class ErrorEventArgs : EventArgs
        {
            public string ErrorMessage { get; }
            public string RequestId { get; }
            public Exception? Exception { get; }

            public ErrorEventArgs(string errorMessage, string requestId = "0000", Exception? exception = null)
            {
                ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
                RequestId = requestId ?? "0000";
                Exception = exception;
            }
        }

        #endregion Event Args Classes

        #region Constructor and Disposal

        public NonBlockingTimer()
        {
            _intervalSeconds = 0;
            _elapsedTimeSeconds = 0;
            _repeat = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                lock (_lockObject)
                {
                    StopInternal(false);
                    _cts.Dispose();
                    _isDisposed = true;
                }
            }
        }

        ~NonBlockingTimer()
        {
            Dispose(false);
        }

        #endregion Constructor and Disposal

        #region Configuration

        public enum IntervalUnit
        {
            Seconds,
            Minutes
        }

        public async Task SetIntervalAsync(double interval, IntervalUnit unit = IntervalUnit.Seconds, bool repeat = false)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(NonBlockingTimer));
            if (double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0)
                throw new ArgumentException("Interval must be a positive finite number.", nameof(interval));

            lock (_lockObject)
            {
                _intervalSeconds = unit switch
                {
                    IntervalUnit.Seconds => interval,
                    IntervalUnit.Minutes => interval * 60,
                    _ => throw new ArgumentException("Invalid interval unit.", nameof(unit))
                };
                _repeat = repeat;
            }
            await RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("interval_set"));
        }

        #endregion Configuration

        #region Core Operations

        public async Task StartAsync()
        {
            if (!_isRunning)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(NonBlockingTimer));

                Task? timerStartedTask = null;
                Task? stateChangedTask = null;

                lock (_lockObject)
                {
                    if (_isRunning)
                    {
                        if (_isPaused)
                        {
                            _isPaused = false;
                            _startTime = DateTime.UtcNow.AddSeconds(-_elapsedTimeSeconds);
                            timerStartedTask = RaiseAsyncEvent(TimerStarted, EventArgs.Empty);
                        }
                        return;
                    }

                    _isRunning = true;
                    _isPaused = false;
                    _elapsedTimeSeconds = 0;
                    _cts = new CancellationTokenSource();
                    _startTime = DateTime.UtcNow;
                    _timerTask = Task.Run(() => TimerLoopAsync(_cts.Token), _cts.Token);

                    // Capture tasks to await outside the lock
                    stateChangedTask = RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("started"));
                    timerStartedTask = RaiseAsyncEvent(TimerStarted, EventArgs.Empty);
                }

                // Await outside the lock
                if (stateChangedTask != null)
                    await stateChangedTask;
                if (timerStartedTask != null)
                    await timerStartedTask;
            }
        }

        public async Task PauseAsync()
        {
            if (_elapsedTimeSeconds > 0)

                if (_isDisposed) throw new ObjectDisposedException(nameof(NonBlockingTimer));
            lock (_lockObject)
            {
                if (!_isRunning || _isPaused) return;
                _isPaused = true;
            }
            await RaiseAsyncEvent(TimerPaused, EventArgs.Empty);
            await RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("paused"));
        }

        public async Task StopAsync()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(NonBlockingTimer));
            lock (_lockObject)
            {
                StopInternal(true);
            }
            await RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("stopped"));
        }

        public async Task ResetAsync()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(NonBlockingTimer));

            Task? stateChangedTask = null;

            lock (_lockObject)
            {
                // If stopped (not running, not paused), reset without starting
                if (!_isRunning && !_isPaused)
                {
                    _elapsedTimeSeconds = 0;
                    stateChangedTask = RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("reset"));
                }
                // If paused (running, paused), reset without starting
                else if (_isRunning && _isPaused)
                {
                    _elapsedTimeSeconds = 0;
                    _isPaused = false; // Optionally keep paused, see note below
                    _isRunning = false; // Don’t resume counting
                    stateChangedTask = RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("reset"));
                }
                // If running (running, not paused), reset without stopping
                else if (_isRunning && !_isPaused)
                {
                    _elapsedTimeSeconds = 0;
                    _startTime = DateTime.UtcNow; // Adjust start time to keep running
                    stateChangedTask = RaiseAsyncEvent(StateChanged, new StateChangedEventArgs("reset"));
                }
            }

            if (stateChangedTask != null)
                await stateChangedTask;
        }

        private void StopInternal(bool raiseStoppedEvent)
        {
            if (!_isRunning) return;
            _isRunning = false;
            _isPaused = false;
            _elapsedTimeSeconds = 0;
            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
            }
            if (raiseStoppedEvent)
                Task.Run(() => RaiseAsyncEvent(TimerStopped, EventArgs.Empty));
        }




        private async Task TimerLoopAsync(CancellationToken token)
        {
            try
            {
                while (_isRunning && !token.IsCancellationRequested)
                {
                    if (_isPaused)
                    {
                        await Task.Delay(100, token);
                        continue;
                    }

                    _elapsedTimeSeconds = (DateTime.UtcNow - _startTime).TotalSeconds;
                    await RaiseAsyncEvent(TimerTick, EventArgs.Empty);

                    if (_elapsedTimeSeconds >= _intervalSeconds)
                    {
                        await RaiseAsyncEvent(TimerCompleted, EventArgs.Empty);
                        if (_repeat)
                        {
                            _elapsedTimeSeconds = 0;
                            _startTime = DateTime.UtcNow;
                            await Task.Delay(100, token);
                        }
                        else
                        {
                            await StopAsync();
                            return;
                        }
                    }
                    await Task.Delay(10, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                await RaiseAsyncEvent(ErrorOccurred, new ErrorEventArgs("Timer loop failed", "0000", ex));
                await StopAsync();
            }
        }

        #endregion Core Operations

        #region Properties

        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public string State => _timerState;

        public double RemainingTimeSeconds
        {
            get
            {
                lock (_lockObject)
                {
                    return _isRunning && !_isPaused
                        ? Math.Max(0, _intervalSeconds - _elapsedTimeSeconds)
                        : _intervalSeconds;
                }
            }
        }

        public string GetTimeString()
        {
            double remaining = RemainingTimeSeconds;
            int minutes = (int)(remaining / 60);
            int seconds = (int)(remaining % 60);
            return $"{minutes:D2}:{seconds:D2}";
        }

        #endregion Properties

        #region Event Raising

        private async Task RaiseAsyncEvent<T>(AsyncEventHandler<T>? handler, T args) where T : EventArgs
        {
            if (_isDisposed || handler == null) return;
            var handlers = handler.GetInvocationList();
            foreach (var h in handlers.Cast<AsyncEventHandler<T>>())
            {
                try
                {
                    await h(this, args);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Handler '{h.Method.Name}' error: {ex.Message}");
                    await RaiseAsyncEvent(ErrorOccurred, new ErrorEventArgs($"Handler failed: {ex.Message}", "0000", ex));
                }
            }
        }

        #endregion Event Raising
    }
}

/*


    public class NonBlockingTimer
    {
        private readonly object lockObject = new object();
        private volatile bool _running = false;
        private volatile bool paused;
        private bool repeat;
        private string _timerState = "stop";

        private DateTime startTime;
        private double interval;
        private double elapsedTime;

        private CancellationTokenSource cancellationSource;

        public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs args);

        public event AsyncEventHandler<StateChangedEventArgs>? StateChnagedEvent;

        public event AsyncEventHandler<ErrorEventArgs>? ErrorEvent;

        public event EventHandler? TimerStarted;

        public event EventHandler? TimerStopped;

        public event EventHandler? TimerCompleted;

        public event EventHandler? TimerTick;

        public event EventHandler? TimerPaused;

        public event EventHandler? TimerStatus;

        protected virtual async Task OnStateChangedAsync(string statusMessage, string requestId = "000")
        {
            var handlers = StateChnagedEvent?.GetInvocationList();
            if (handlers == null) return;

            foreach (var handler in handlers.Cast<AsyncEventHandler<StateChangedEventArgs>>())
            {
                try
                {
                    _timerState = statusMessage;

                    await handler(this, new StateChangedEventArgs(statusMessage, requestId));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Handler '{handler.Method.Name}' error: {ex.MessageAnthropic}");
                }
            }
        }

        protected virtual async Task OnErrorAsync(string errorMessage, string requestId)
        {
            var handlers = ErrorEvent?.GetInvocationList();
            if (handlers == null) return;

            foreach (var handler in handlers.Cast<AsyncEventHandler<ErrorEventArgs>>())
            {
                try
                {
                    await handler(this, new ErrorEventArgs(errorMessage, requestId));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Handler '{handler.Method.Name}' error: {ex.MessageAnthropic}");
                }
            }
        }

        public class StateChangedEventArgs : EventArgs
        {
            public string MessageAnthropic { get; }
            public string RequestId { get; }

            public StateChangedEventArgs(string statusMessage, string requestId = "0000")
            {
                MessageAnthropic = statusMessage;
                RequestId = requestId;
            }
        }

        public class ErrorEventArgs : EventArgs
        {
            public string ErrorMessage { get; }
            public string RequestId { get; }

            public ErrorEventArgs(string errorMessage, string requestId = "0000")
            {
                ErrorMessage = errorMessage;
                RequestId = requestId;
            }
        }

        public NonBlockingTimer()
        {
            interval = 0;
            _running = false;
            paused = false;
            elapsedTime = 0;
            cancellationSource = new CancellationTokenSource();
            repeat = false; // Default to non-repeating
        }

        public enum IntervalUnit
        {
            Seconds,
            Minutes
        }

        public async void SetInterval(double interval, IntervalUnit intervalUnit = IntervalUnit.Seconds, bool repeat = false)
        {
            switch (intervalUnit)
            {
                case IntervalUnit.Seconds:
                    this.interval = interval;
                    break;

                case IntervalUnit.Minutes:
                    this.interval = interval * 60;
                    break;

                default:
                    await OnErrorAsync("Invalid interval unit", "0000");
                    break;
            }

            this.repeat = repeat;
        }

        private async Task TimerLoopAsync()
        {
            try
            {
                TimerStarted?.Invoke(this, EventArgs.Empty);
                startTime = DateTime.Now.AddSeconds(-elapsedTime);

                while (_running)
                {
                    if (cancellationSource.Token.IsCancellationRequested)
                        break;

                    if (!paused)
                    {
                        elapsedTime = (DateTime.Now - startTime).TotalSeconds;
                        if (elapsedTime >= interval)
                        {
                            TimerCompleted?.Invoke(this, EventArgs.Empty);

                            if (!repeat)
                            {
                                Stop();
                                break;
                            }

                            // Reset for next interval
                            elapsedTime = 0;
                            startTime = DateTime.Now;

                            await Task.Delay(10000); // 10 second cooldown
                        }
                    }
                    await Task.Delay(100);
                }
            }
            catch (Exception)
            {
                Stop();
            }
            finally
            {
                TimerStopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool Running
        {
            get => _running;
        }

        public string State
        {
            get => _timerState;
        }

        public async void Start()
        {
            lock (lockObject)
            {
                if (!_running)
                {
                    _running = true;
                    paused = false;
                    cancellationSource = new CancellationTokenSource();
                    Task.Run(TimerLoopAsync);
                }
                else if (paused)
                {
                    paused = false;
                    startTime = DateTime.Now.AddSeconds(-elapsedTime);
                }
            }
            await OnStateChangedAsync("start");
        }

        public async void Pause()
        {
            if (_running && !paused)
            {
                paused = true;
                TimerPaused?.Invoke(this, EventArgs.Empty);
            }
            await OnStateChangedAsync("pause");
        }

        public async void Stop()
        {
            lock (lockObject)
            {
                if (_running)
                {
                    _running = false;
                    paused = false;
                    elapsedTime = 0;
                    cancellationSource.Cancel();
                }
            }
            await OnStateChangedAsync("stop");
        }

        public async void Reset()
        {
            lock (lockObject)
            {
                if (_running)
                {
                    _running = false;
                    paused = false;
                    elapsedTime = 0;
                    cancellationSource.Cancel();
                }
                else
                {
                        _running = true;
                        paused = false;
                        elapsedTime = 0;
                        cancellationSource = new CancellationTokenSource();
                        Task.Run(TimerLoopAsync);
                }
            }
            await OnStateChangedAsync("reset");
        }
        public string GetTimeString()
        {
            double remaining = GetRemainingTime();
            int minutes = (int)(remaining / 60);
            int seconds = (int)(remaining % 60);
            return $"{minutes:D2}:{seconds:D2}";
        }

        private double GetRemainingTime()
        {
            if (!_running)
                return interval;

            double remaining = interval - elapsedTime;
            return Math.Max(0, remaining);
        }
    }



*/
/*


        //  private NonBlockingTimer _timerCacheAlive;

        private void InitializeTimer()
        {
            _timerCacheAlive = new NonBlockingTimer();

            // Attach event handlers
            _timerCacheAlive.TimerStarted += Timer_TimerStarted;
            _timerCacheAlive.TimerStopped += Timer_TimerStopped;
            _timerCacheAlive.TimerCompleted += Timer_TimerCompleted;
            _timerCacheAlive.TimerTick += Timer_TimerTick;
            _timerCacheAlive.TimerPaused += Timer_TimerPaused;
            _timerCacheAlive.SetInterval(4.25, IntervalUnit.Minutes, repeat: false);
            //_timerCacheAlive.SetInterval(15, IntervalUnit.Seconds, repeat: false);
        }

        private void btnStartTimer_Click(object sender, EventArgs e)
        {
            _timerCacheAlive.Start();
        }

        private void btnStopTimer_Click(object sender, EventArgs e)
        {
            _timerCacheAlive.Stop();
        }

        private void btnResetTimer_Click(object sender, EventArgs e)
        {
            _timerCacheAlive.Reset();
        }

        private void btnPauseTimer_Click(object sender, EventArgs e)
        {
            _timerCacheAlive.Pause();
        }

        private void Timer_TimerStarted(object? sender, EventArgs e)
        {
            // LogMessage($"Timer started! - Time remaining: {_timerCacheAlive.GetTimeString()}");
        }

        private void Timer_TimerPaused(object? sender, EventArgs e)
        {
            // LogMessage($"Timer paused! - Time remaining: {_timerCacheAlive.GetTimeString()}");
        }

        private void Timer_TimerStopped(object? sender, EventArgs e)
        {
            // LogMessage("Timer stopped!");
        }

        private async void Timer_TimerCompleted(object? sender, EventArgs e)
        {
            // LogMessage("Timer stopped!");
        }

        private async void Timer_TimerTick(object? sender, EventArgs e)
        {
             // LogMessage("Timer stopped!");
        }



*/