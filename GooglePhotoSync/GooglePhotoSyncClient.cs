using Google.Apis.Auth.OAuth2;
using Google.Apis.PhotosLibrary.v1;
using Google.Apis.PhotosLibrary.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace GooglePhotoSync
{
    public class GooglePhotoSyncClient
    {
        internal static string[] Scopes = { PhotosLibraryService.Scope.PhotoslibraryReadonly };
        private const string _applicationName = "GooglePhotoSyncByLolo";

        private UserCredential _credential;

        public GooglePhotoSyncClientSettings Settings { get; set; }

        public ObservableCollection<DownloadItem> DownloadItems { get; private set; }

        private LimitedConcurrencyLevelTaskScheduler _downloadTaskScheduler;

        public bool RemoveItemWhenDowloaded { get; set; }

        private string _settingsFolder = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _applicationName);

        public GooglePhotoSyncClient()
        {
            Settings = new GooglePhotoSyncClientSettings() { LocalPhotosPath = ".\\", ConcurrentDownloads = 4 };
            DownloadItems = new ObservableCollection<DownloadItem>();
            RemoveItemWhenDowloaded = false;
        }

        public async Task LogonAsync()
        {
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GetLocalClientSecrets(),
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(_settingsFolder, true));
        }

        public void LoadSettings()
        {
            using (var reader = new StreamReader(Path.Combine(_settingsFolder, "settings.json")))
            {
                var serializer = new Newtonsoft.Json.JsonSerializer();
                Settings = serializer.Deserialize<GooglePhotoSyncClientSettings>(new Newtonsoft.Json.JsonTextReader(reader));
            }
        }

        public void SaveSettings()
        {
            Directory.CreateDirectory(_settingsFolder);

            using (var writer = new StreamWriter(Path.Combine(_settingsFolder, "settings.json")))
            {
                var serializer = new Newtonsoft.Json.JsonSerializer();
                serializer.Serialize(new Newtonsoft.Json.JsonTextWriter(writer), Settings);
            }
        }

        public class GooglePhotoSyncClientSettings
        {
            public string LocalPhotosPath { get; set; }
            public int ConcurrentDownloads { get; set; }
        }

        public ClientSecrets GetLocalClientSecrets()
        {
            using (var stream = new FileStream(@"client_secret.json", FileMode.Open, FileAccess.Read))
            {
                return GoogleClientSecrets.Load(stream).Secrets;
            }
        }

        //public async Task SyncConsoleAsync()
        //{
        //    var allItems = await GetAllMediaItemsAsync();

        //    DownloadItems = allItems.Where(m => !DownloadItem.AllreadyDownloaded(m, SyncDirectory))
        //        .Select(p => DownloadItem.CreateDownloadItem(p, SyncDirectory))
        //        .ToList();

        //    TaskFactory factory = new TaskFactory(_downloadTaskScheduler);

        //    // start all using limited task scheduler
        //    foreach (var item in DownloadItems)
        //        item.DownloadAsync(factory).ConfigureAwait(false);

        //    do
        //    {
        //        Console.Clear();
        //        foreach (var item in DownloadItems)
        //        {
        //            Console.WriteLine($"{item.MediaItem.Filename} {item.Completed} {item.Exception?.Message} {item.Downloading} ");
        //        }
        //    }
        //    while (!WaitAll(500));
        //}

        public bool WaitAll(int timeout = 0)
        {
            return Task.WaitAll(DownloadItems.Select(p => p.DownloadTask).ToArray(), timeout);
        }

        public Task StartDownloadAsync()
        {
            if (Settings.ConcurrentDownloads == 0)
                Settings.ConcurrentDownloads = 4;
            _downloadTaskScheduler = new LimitedConcurrencyLevelTaskScheduler(Settings.ConcurrentDownloads);
            TaskFactory factory = new TaskFactory(_downloadTaskScheduler);

            var syncContext = SynchronizationContext.Current;

            // start all using limited task scheduler
            foreach (var item in DownloadItems)
            {
                item.DownloadAsync(factory).ContinueWith((task) => {
                    if (RemoveItemWhenDowloaded && item.Completed == true && item.Failed == false)
                    {
                        syncContext.Send((o) => { DownloadItems.Remove(item); }, null);
                    }
                });
            }

            return Task.Run(() =>
            {
                WaitAll();
            });
        }

        public async Task FillDownloadItemsAsync()
        {
            var allItems = await GetAllMediaItemsAsync();

            DownloadItems.Clear();

            foreach (var item in allItems.Where(m => !DownloadItem.AllreadyDownloaded(m, Settings.LocalPhotosPath)))
                DownloadItems.Add(DownloadItem.CreateDownloadItem(item, Settings.LocalPhotosPath));
        }

        public async Task<List<MediaItem>> GetAllMediaItemsAsync()
        {
            List<MediaItem> items = new List<MediaItem>();

            using (var photoService = new PhotosLibraryService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = _applicationName
            }))
            {
                string nextPageToken = string.Empty;

                do
                {
                    var searchRequest = photoService.MediaItems.Search(new SearchMediaItemsRequest() { PageSize = 100, PageToken = nextPageToken });
                    var result = await searchRequest.ExecuteAsync();

                    items.AddRange(result.MediaItems);

                    nextPageToken = result.NextPageToken;
                }
                while (nextPageToken != null);
            }

            return items;
        }
    }
}
