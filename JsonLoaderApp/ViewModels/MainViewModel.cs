using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JsonLoaderApp.Models;
using JsonLoaderApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;


namespace JsonLoaderApp.ViewModels
{
    public partial class MainViewModel : ObservableObject, IAsyncDisposable
    {
        private readonly Dispatcher _dispatcher;
        private IFilePollingService _fileService;

        private readonly HashSet<string> _messages = new();

        [ObservableProperty]
        private bool canRead = true;

        [ObservableProperty]
        private ObservableCollection<SensorData> dataset = new();

        [RelayCommand(CanExecute = nameof(CanRead))]
        private async Task ManualDataUpdate()
        {
            await _fileService.ManualUpdate();
        }

        partial void OnCanReadChanged(bool value)
        {
            ManualDataUpdateCommand.NotifyCanExecuteChanged();
        }

        public MainViewModel(Dispatcher dispatcher, IFilePollingService filePollingService)
        {
            _dispatcher = dispatcher;

            _fileService = filePollingService;
            _fileService.FileChanged += OnDataChanged;
            _fileService.UserMessage += OnDisplayMessage;
            _fileService.Start();
        }

        private void OnDataChanged(object? sender, List<SensorData> contents)
        {
            _ = _dispatcher.InvokeAsync(() =>
            {
                Dataset = new ObservableCollection<SensorData>(contents);
            });
        }
        //This should be connected to a message service (and used a model for data) if the application was for production. 

        private void OnDisplayMessage(object? sender, string message)
        {
            if (!_messages.Add(message))
                return;
            try
            {
                MessageBox.Show(message);
            }
            finally
            {
                _messages.Remove(message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _fileService.FileChanged -= OnDataChanged;
            _fileService.UserMessage -= OnDisplayMessage;
            await _fileService.DisposeAsync();
        }
    }
}
