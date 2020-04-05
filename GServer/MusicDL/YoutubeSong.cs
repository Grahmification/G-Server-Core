using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using YoutubeExplode;
using YoutubeExplode.Models;
using YoutubeExplode.Models.MediaStreams;

namespace GServer.MusicDL
{
    public class YoutubeVideo
    {
        //downloads here if no folder specified
        private const string defaultDlFolder_Windows = @"C:\Users\Graham\Desktop\YoutubeDlMusic";
        private const string defaultDlFolder_Linux = @"/srv/Music - YoutubeDownloaded";

        public static string DefaultDlFolder
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))//detect the OS        
                    return YoutubeVideo.defaultDlFolder_Windows; //set windows directory
                else
                    return YoutubeVideo.defaultDlFolder_Linux; //set Linux directory
            }
        }
        public MediaStreamInfoSet StreamInfoSet { get; private set; } = null;
        public Video Video { get; private set; }

        public string Link { get; private set; } = "";

        private string videoID = "";

        public YoutubeVideo(string Link)
        {
            this.Link = Link;
            videoID = YoutubeClient.ParseVideoId(Link);
            var client = new YoutubeClient();
            
            // Get metadata for all streams in this video - now done at download time
            //this.StreamInfoSet = client.GetVideoMediaStreamInfosAsync(videoID).Result;
            
            // Get title of video, etc
            var videoTask = client.GetVideoAsync(videoID);
            videoTask.Wait();
            this.Video = videoTask.Result;
        }
        public async Task<string> DownloadAudioNative(string folderPath, string fileName = "")
        {
            var client = new YoutubeClient();

            if (StreamInfoSet == null)               
                StreamInfoSet = await client.GetVideoMediaStreamInfosAsync(videoID); // Get metadata for all streams in this video
           
            // Select one of the streams, e.g. highest quality muxed stream
            var streamInfo = this.StreamInfoSet.Audio.WithHighestBitrate();

            // Get file extension based on stream's container
            var ext = streamInfo.Container.GetFileExtension();

            if (fileName == "")
                fileName = this.Video.Title; //set filename to the video title if not specified

            fileName = $"{fileName}.{ext}"; //add the extension to the filename
            string filePath = Path.Combine(folderPath, fileName);

            filePath = MusicTagging.ReplaceInvalidChars(filePath); //replace any invalid chars in filePath with valid
            filePath = MusicTagging.UpdateFileNameForDuplicates(filePath); //add "copy" to filename if file exists

            var fs = File.Create(filePath);
            await client.DownloadMediaStreamAsync(streamInfo, fs); //wait for the download to finish

            fs.Close();

            return filePath;
        }
        public async Task<string> DownloadAudioMP3(string folderPath, string fileName = "")
        {
            if(StreamInfoSet == null)
            {
                var client = new YoutubeClient();           
                StreamInfoSet = await client.GetVideoMediaStreamInfosAsync(videoID); // Get metadata for all streams in this video
            }

            
            
            if (fileName == "")
                fileName = this.Video.Title; //set filename to the video title if not specified

            string filePath = Path.Combine(folderPath, fileName);

            // Select one of the streams, e.g. highest quality muxed stream
            var streamInfo = this.StreamInfoSet.Audio.WithHighestBitrate();

            if (this.StreamInfoSet.Audio.Count == 0) //sometimes the libary can't seem to find the audio
                throw new Exception("Failed to locate audio in chosen video.");

            var savePath = await MusicConverting.YoutubeAudioToMP3(streamInfo.Url, streamInfo.AudioEncoding.ToString(), this.Video.Duration, filePath);

            return savePath;
        }
        public async Task TagMP3File(string filePath)
        {
            var tfile = TagLib.File.Create(filePath);
            string title = tfile.Tag.Title;

            // change title only if one doesn't exist
            if (tfile.Tag.Title == "" || tfile.Tag.Title == null)
                tfile.Tag.Title = this.Video.Title;

            // add video thumbnail
            var thumbUrl = this.Video.Thumbnails.HighResUrl;
            if (thumbUrl != "" && thumbUrl != null)
                MusicTagging.addPictureNoSave(tfile, await MBServer.GetImage(thumbUrl));

            tfile.Save();
        }

        public static string EmbedLink(string Link)
        {
            if (Link != "")
                try
                {
                    return "https://www.youtube.com/embed/" + YoutubeClient.ParseVideoId(Link);
                }
                catch (FormatException) //link was invalid
                {
                    return "";
                }
            else
                return "";
        }
        public static string LinkFromID(string ID)
        {
            return "https://www.youtube.com/watch?v=" + ID;
        }
    }

    public class YoutubeVideoDL
    {
        public DownloadStates Status { get; private set; } = DownloadStates.Idle;
        public string StatusString
        {
            get
            {
                switch (Status)
                {
                    case DownloadStates.DownloadComplete:
                        return "Download Complete";

                    case DownloadStates.Downloading:
                        return "Downloading...";

                    case DownloadStates.DownloadStopped:
                        return "Stopped";

                    case DownloadStates.Error:
                        return "Download Error";

                    case DownloadStates.Idle:
                        return "Awaiting Download";

                    default:
                        return "Download Error";
                }
            }
        } //user friendly strings for each state
        public string ErrorText { get; private set; } = "";
        public YoutubeVideo Video { get; private set; } = null;
        public Song TaggedSong { get; private set; } = null;
        public Release TaggedSongAlbum
        {
            get { return TaggedSong.Releases[_taggedSongReleaseIndex];  }
        }
        public DateTime StartTime { get; private set; } = new DateTime();


        public event Action OnChange;

        private int _taggedSongReleaseIndex = -1; 
        
        public YoutubeVideoDL(string link, Song taggedSong = null, int taggedSongReleaseIndex = -1)
        {
            TaggedSong = taggedSong;
            _taggedSongReleaseIndex = taggedSongReleaseIndex;

            if (link == "")
            {
                this.Status = DownloadStates.Error;
                this.ErrorText = "No Link Specified";
                return;
            }
      
            Video = new YoutubeVideo(link);
        }
        public async Task Download()
        {
            try
            {
                StartTime = DateTime.Now;
                Status = DownloadStates.Downloading;
                OnChange?.Invoke();

                var savePath = await Video.DownloadAudioMP3(YoutubeVideo.DefaultDlFolder);
                await Video.TagMP3File(savePath);
                
                if (TaggedSong != null)
                   await TaggedSong.TagMP3File(savePath, _taggedSongReleaseIndex);

                MusicTagging.Folderize(savePath, YoutubeVideo.DefaultDlFolder);

                this.Status = DownloadStates.DownloadComplete;
            }
            catch (Exception ex)
            {
                Status = DownloadStates.Error;
                ErrorText = ex.ToString();
            }
            finally
            {
                OnChange?.Invoke(); //raise event that status has changed
            }
        }

        public enum DownloadStates
        {
            Idle, Downloading, DownloadComplete, Error, DownloadStopped
        }
    }

    public class SongDownloadManagerClass
    {
        public IList<YoutubeVideoDL> downloads { get; private set; } = new List<YoutubeVideoDL>();

        public event Action OnChange;

        public async Task AddDownload(YoutubeVideoDL download)
        {
            downloads.Add(download);
            NotifyStateChanged(); //not really needed since status changes of download

            download.OnChange += NotifyStateChanged;
            await download.Download();
        }
        public void RemoveDownload(int index)
        {
            var dl = downloads[index];
            downloads.RemoveAt(index);

            dl.OnChange -= NotifyStateChanged;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

    }

}
