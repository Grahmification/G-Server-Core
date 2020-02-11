using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Streams;
using System.Runtime.InteropServices;

namespace GServer.MusicDL
{
    public class MusicConverting
    {
        public const string FFmpegLibraryPath_Windows = @"C:\Users\Graham\Desktop\ffmpeg-20191111-20c5f4d-win64-static\bin"; //windows location
        public const string FFmpegLibraryPath_Linux = @"/usr/bin"; //linux location

        public static string YoutubeAudioToMP3(string audioLink, string audioFormat, TimeSpan audioDuration, string filePath)
        {
            //Save file to the same location with changed extension
            string outputFilePath = Path.ChangeExtension(filePath, ".mp3");

            outputFilePath = MusicTagging.ReplaceInvalidChars(outputFilePath); //replace any invalid chars in filePath with valid
            outputFilePath = MusicTagging.UpdateFileNameForDuplicates(outputFilePath); //add "copy" to filename if file exists


            //------------ Set library directory --------------
            MusicConverting.setFFMPEGPath();

            //----- Get youtube video audio stream -----------------
            IStream audioStream = new WebStream(new Uri(audioLink), audioFormat, audioDuration);

            //--- do the conversion ----------------
            convertAudio(audioStream, outputFilePath);

            return outputFilePath;
        }
        public static async Task<string> AudioFileToMP3(string filePath, bool deleteOriginal = true)
        {
            if (!File.Exists(filePath))
                throw new Exception("Couldn't locate file to convert.");

            //Save file to the same location with changed extension
            string outputFilePath = Path.ChangeExtension(filePath, ".mp3");
            outputFilePath = MusicTagging.ReplaceInvalidChars(outputFilePath); //replace any invalid chars in filePath with valid
            outputFilePath = MusicTagging.UpdateFileNameForDuplicates(outputFilePath); //add "copy" to filename if file exists


            //------------ Set library directory --------------
            MusicConverting.setFFMPEGPath();


            //get info of input file
            IMediaInfo mediaInfo = await MediaInfo.Get(filePath);
            IStream audioStream = mediaInfo.AudioStreams.FirstOrDefault();

            //------------ do the conversion ----------------
            convertAudio(audioStream, outputFilePath);


            if (deleteOriginal)
                File.Delete(filePath); //delete the original file

            return outputFilePath;
        }

        private static void convertAudio(IStream audioStream, string outputFilePath)
        {
            var conv = new Conversion();
            conv.AddStream(audioStream);
            conv.SetOutput(outputFilePath);

            //constant bitrate needed to stop song length from doubling incorrectly - FFMpeg bug
            //see here https://superuser.com/questions/892996/ffmpeg-is-doubling-audio-length-when-extracting-from-video/893044
            //128k seems to be typical youtube audio quality - won't increase file size
            conv.SetAudioBitrate("128k");
            var convTask = conv.Start();
            convTask.Wait(); //wait for conversion to finish; 
        }
        private static void setFFMPEGPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))//detect the OS        
                FFmpeg.ExecutablesPath = new DirectoryInfo(FFmpegLibraryPath_Windows).FullName; //set windows directory
            else
                FFmpeg.ExecutablesPath = new DirectoryInfo(FFmpegLibraryPath_Linux).FullName; //set Linux directory
        }

    }
}
