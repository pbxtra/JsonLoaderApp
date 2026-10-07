using JsonLoaderApp.Models;
using System.IO;
using System.Text.Json;

namespace JsonLoaderApp.Services
{
    public sealed class FilePollingService : IFilePollingService, IDisposable, IAsyncDisposable
    {
        private readonly Action? _afterCheck; //This is for testing 
        private readonly SemaphoreSlim _checkLock = new(1, 1);
        private readonly CancellationTokenSource _cancellationSource = new();
        private readonly TimeSpan _interval;
        private readonly string _path;
        private Task? _pollingTask;
        private readonly TimeProvider _timerProvider;
        private int _disposed;
        private int _ioProblem = 0;
        private int _retrys = 6;
        private int _notFoud = 0;
        private int _started;


        private DateTime _lastModifiedTimestamp = DateTime.MinValue;

        public event EventHandler<List<SensorData>>? FileChanged;
        public event EventHandler<string>? UserMessage;


        public FilePollingService(string path, TimeSpan? interval = null, TimeProvider? timeProvider = null, Action? afterCheck = null)
        {
            _path = path;
            _interval = interval ?? TimeSpan.FromSeconds(2);
            _timerProvider = timeProvider ?? TimeProvider.System;
            _afterCheck = afterCheck;
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
                throw new InvalidOperationException("Service is already started");

            _pollingTask = PollAsync(_cancellationSource.Token, _timerProvider);
        }

        private async Task PollAsync(CancellationToken cancellationToken, TimeProvider timeProvider)
        {
            using var timer = new PeriodicTimer(_interval, timeProvider);

            try
            {
                await CheckFileSafelyAsync(cancellationToken);
                _afterCheck?.Invoke();
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        await CheckFileSafelyAsync(cancellationToken);
                    }
                    finally
                    {
                        _afterCheck?.Invoke();
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                //Expected 
            }
        }

        private async Task CheckFileSafelyAsync(CancellationToken cancellationToken)
        {
            await _checkLock.WaitAsync(cancellationToken);
            try
            {
                await CheckFileAsync(cancellationToken);

            }
            finally
            {
                _checkLock.Release();

            }
        }

        private async Task CheckFileAsync(CancellationToken cancellationToken)
        {
            try
            {
                var modifiedTimestamp = File.GetLastWriteTimeUtc(_path);

                if (modifiedTimestamp <= _lastModifiedTimestamp)
                    return;

                var contents = await File.ReadAllTextAsync(_path, cancellationToken);
                _lastModifiedTimestamp = modifiedTimestamp;

                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var parced = JsonSerializer.Deserialize<SensorDataCollection>(contents, opts);
                if (parced != null)
                {
                    FileChanged?.Invoke(this, parced.SensorDataList);
                }
            }

            //TODO: this is just a simple error handlign implementation . Just so we have something.
            //Would be handled by a message service in production, I don't have time right now.
            catch (FileNotFoundException)
            {
                //File missing , send message to user after a few loops
                _notFoud++;

                if (_notFoud >= _retrys)
                {
                    UserMessage?.Invoke(this, "File missing from folder");
                    _notFoud = 0;
                }

            }
            catch (IOException)
            {
                //File read error , send message to user after a few loops
                _ioProblem++;

                if (_ioProblem >= _retrys)
                {
                    UserMessage?.Invoke(this, "Could not read file!");
                    _ioProblem = 0;
                }

            }
            catch (Exception ex)
            {
                //We should not end up here, but if we do, we will send a message to the user. 
                UserMessage?.Invoke(this, "Well, this should not happen: " + ex.Message);
            }
        }

        public async Task<bool> ManualUpdate()
        {
            await CheckFileSafelyAsync(_cancellationSource.Token);
            return true;
        }


        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _cancellationSource.Cancel();
            _cancellationSource.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await _cancellationSource.CancelAsync();
            if (_pollingTask is not null)
            {
                await _pollingTask;
            }

            _cancellationSource.Dispose();
        }
    }
}
