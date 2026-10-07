using JsonLoaderApp.Services;
using JsonLoaderApp.ViewModels;
using System.Windows;


namespace JsonLoaderApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var fileService = new FilePollingService("data.json");
            DataContext = new MainViewModel(Dispatcher, fileService);
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            (DataContext as MainViewModel)?.DisposeAsync();
        }
    }
}