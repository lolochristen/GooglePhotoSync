using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GooglePhotoSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GooglePhotoSyncClient _syncClient;

        public MainWindow()
        {
            InitializeComponent();
            _syncClient = new GooglePhotoSyncClient();
            try
            {
                _syncClient.LoadSettings();
            }
            catch { }
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;
            Sync().ConfigureAwait(false);
        }

        private async Task Sync()
        {
            try
            {
                InfoText.Text = "Logon...";
                await _syncClient.LogonAsync();

                InfoText.Text = "Get files to download...";
                Progress.Value = 1;

                await _syncClient.FillDownloadItemsAsync();

                Progress.Maximum = _syncClient.DownloadItems.Count;
                _syncClient.DownloadItems.CollectionChanged += (sender, args) =>
                {
                    Progress.Value = Progress.Maximum - _syncClient.DownloadItems.Count;
                };

                Dispatcher.Invoke(() =>
                {
                    ProgressGrid.ItemsSource = _syncClient.DownloadItems;
                });

                InfoText.Text = "Downloading...";
                await _syncClient.StartDownloadAsync();

                Dispatcher.Invoke(() => { SyncButton.IsEnabled = true; });
            }
            catch(Exception exp)
            {
                MessageBox.Show("Error executing synchronization.\n" + exp.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InfoText.Text = "done.";
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.LocalPhotosPath = _syncClient.Settings.LocalPhotosPath;
            if (settingsWindow.ShowDialog() == true)
            {
                // save 
                _syncClient.Settings.LocalPhotosPath = settingsWindow.LocalPhotosPath;
                _syncClient.SaveSettings();
            }
        }
    }
}
