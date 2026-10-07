using JsonLoaderApp.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace JsonLoaderApp.Services
{
    public interface IFilePollingService
    {
        public event EventHandler<List<SensorData>>? FileChanged;
        public event EventHandler<string>? UserMessage;
        public Task<bool> ManualUpdate();
        public void Start();

        public ValueTask DisposeAsync();
    }
}
