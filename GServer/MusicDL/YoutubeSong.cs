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
        public MediaStreamInfoSet StreamInfoSet { get; private set; }
        public Video Video { get; private set; }


        public YoutubeVideo(string Link)
        {
            string videoID = YoutubeClient.ParseVideoId(Link);
            var client = new YoutubeClient();

            // Get metadata for all streams in this video
            this.StreamInfoSet = client.GetVideoMediaStreamInfosAsync(videoID).Result;

            // Get tite of video, etc
            var videoTask = client.GetVideoAsync(videoID);
            videoTask.Wait();
            this.Video = videoTask.Result;
        }
        public async Task<string> DownloadAudioNative(string folderPath, string fileName = "")
        {
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
            var client = new YoutubeClient();
            await client.DownloadMediaStreamAsync(streamInfo, fs); //wait for the download to finish

            fs.Close();

            return filePath;
        }
        public string DownloadAudioMP3(string folderPath, string fileName = "")
        {
            if (fileName == "")
                fileName = this.Video.Title; //set filename to the video title if not specified

            string filePath = Path.Combine(folderPath, fileName);

            // Select one of the streams, e.g. highest quality muxed stream
            var streamInfo = this.StreamInfoSet.Audio.WithHighestBitrate();

            if (this.StreamInfoSet.Audio.Count == 0) //sometimes the libary can't seem to find the audio
                throw new Exception("Failed to locate audio in chosen video.");

            var savePath = MusicConverting.YoutubeAudioToMP3(streamInfo.Url, streamInfo.AudioEncoding.ToString(), this.Video.Duration, filePath);

            return savePath;
        }
        public void TagMP3File(string filePath)
        {
            var tfile = TagLib.File.Create(filePath);
            string title = tfile.Tag.Title;

            // change title only if one doesn't exist
            if (tfile.Tag.Title == "" || tfile.Tag.Title == null)
                tfile.Tag.Title = this.Video.Title;

            // add video thumbnail
            var thumbUrl = this.Video.Thumbnails.HighResUrl;
            if (thumbUrl != "" && thumbUrl != null)
                MusicTagging.addPictureNoSave(tfile, MBServer.GetImage(thumbUrl));

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
}
