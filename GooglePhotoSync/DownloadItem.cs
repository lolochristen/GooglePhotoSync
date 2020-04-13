using Google.Apis.PhotosLibrary.v1.Data;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GooglePhotoSync
{
    public class DownloadItem : INotifyPropertyChanged
    {
        private bool downloading;
        private bool completed;
        private bool failed;
        private bool successfull;
        private int progress;

        public Task DownloadTask { get; private set; }

        public string Name => MediaItem.Filename;

        public bool Completed
        {
            get => completed; set
            {
                completed = value;
                OnPropertyChanged();
            }
        } // => DownloadTask == null ? false : DownloadTask.IsCompleted;

        public bool Failed
        {
            get => failed; private set
            {
                failed = value;
                OnPropertyChanged();
            }
        }

        public bool Successfull
        {
            get => successfull; set
            {
                successfull = value;
                OnPropertyChanged();
            }
        }

        public MediaItem MediaItem { get; private set; }

        public string TargetPath { get; set; }

        public Exception Exception => DownloadTask?.Exception;

        public string ErrorText => Exception.Message;

        public int Progress
        {
            get => progress; set
            {
                progress = value;
                OnPropertyChanged();
            }
        }

        public bool Downloading
        {
            get => downloading; private set
            {
                downloading = value;
                OnPropertyChanged();
            }
        }

        private DownloadItem(MediaItem mediaItem, string rootPath)
        {
            MediaItem = mediaItem;
            TargetPath = BuildPath(mediaItem, rootPath);
            Completed = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static bool AllreadyDownloaded(MediaItem mediaItem, string rootPath)
        {
            return File.Exists(BuildPath(mediaItem, rootPath));
        }

        public static DownloadItem CreateDownloadItem(MediaItem mediaItem, string rootPath)
        {
            var item = new DownloadItem(mediaItem, rootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(item.TargetPath));
            return item;
        }

        public Task DownloadAsync(TaskFactory taskFactory)
        {
            DownloadTask = taskFactory.StartNew(() =>
            {
                // nested to get async working
                Task.Run(async () =>
                {
                    try
                    {
                        var url = MediaItem.BaseUrl + "=d";
                        using (var client = new HttpClient())
                        {
                            Downloading = true;
                            Progress = 5;
                            var response = await client.GetAsync(url);

                            Progress = 10;
                            var stream = await response.Content.ReadAsStreamAsync();
                            var total = 0;
                            var buf = new byte[2048];

                            using (var file = new FileStream(TargetPath, FileMode.Create, FileAccess.Write))
                            {
                                int read;
                                do
                                {
                                    read = await stream.ReadAsync(buf, 0, buf.Length);
                                    if (read > 0)
                                        await file.WriteAsync(buf, 0, read);
                                    total += read;
                                    Progress = (int) (90d / stream.Length * total) + 10;
                                }
                                while (read > 0);
                                //await stream.CopyToAsync(file);
                            }

                            Successfull = true;
                        }
                    }
                    catch //(Exception)
                    {
                        Failed = true;
                        throw;
                    }
                    finally
                    {
                        Downloading = false;
                        Completed = true;
                    }
                }).Wait();

            });
            return DownloadTask;
        }

        private static string BuildPath(MediaItem mediaItem, string rootPath)
        {
            DateTime creationTime;
            if (mediaItem.MediaMetadata.CreationTime is DateTime)
                creationTime = (DateTime)mediaItem.MediaMetadata.CreationTime;
            else
                creationTime = DateTime.Parse(mediaItem.MediaMetadata.CreationTime.ToString());
            return Path.Combine(rootPath, creationTime.Year.ToString(), mediaItem.Filename);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
