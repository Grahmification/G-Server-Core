using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using TagLib.Id3v2;

namespace GServer.MusicDL
{
    public class MusicTagging
    {
        public static void addTag(string Title, string Artist, string filePath)
        {
            var tfile = TagLib.File.Create(filePath);
            string title = tfile.Tag.Title;
            TimeSpan duration = tfile.Properties.Duration;
            Console.WriteLine("Title: {0}, duration: {1}", title, duration);

            // change title in the file
            tfile.Tag.Title = Title;
            tfile.Tag.Performers = new String[1] { Artist };
            tfile.Save();



        }


        public static void addPicture(string filePath, byte[] imageData)
        {
            var targetMp3File = TagLib.File.Create(filePath);

            addPictureNoSave(targetMp3File, imageData);
            targetMp3File.Save();
        }
        public static void addPictureNoSave(TagLib.File targetMp3File, byte[] imageData)
        {
            if (imageData == null)
                return;

            if (imageData.Length == 0)
                return;

            // define picture
            TagLib.Id3v2.AttachedPictureFrame pic = new TagLib.Id3v2.AttachedPictureFrame();
            pic.TextEncoding = TagLib.StringType.Latin1;
            pic.MimeType = System.Net.Mime.MediaTypeNames.Image.Jpeg;
            pic.Type = TagLib.PictureType.FrontCover;
            pic.Data = new TagLib.ByteVector(imageData, imageData.Length);


            // save picture to file
            targetMp3File.Tag.Pictures = new TagLib.IPicture[1] { pic };
        }

        public static void Folderize(string filePath, string baseLibraryFolder)
        {
            var tfile = TagLib.File.Create(filePath);
            string title = tfile.Tag.Title;
            string Artist = tfile.Tag.FirstPerformer;
            string Album = tfile.Tag.Album;
            tfile.Dispose();

            var fileDI = Directory.CreateDirectory(baseLibraryFolder);

            if (Artist != null && Artist != "")
                fileDI = Directory.CreateDirectory(Path.Combine(baseLibraryFolder, ReplaceInvalidChars(Artist)));

            if (Album != null && Album != "")
                fileDI = Directory.CreateDirectory(Path.Combine(fileDI.FullName, ReplaceInvalidChars(Album)));


            string fileName = Path.GetFileName(filePath);

            if (title != null && title != "")
                fileName = $"{ReplaceInvalidChars(title)}{Path.GetExtension(filePath)}";

            string newPath = Path.Combine(fileDI.FullName, fileName);

            if (!File.Exists(newPath))
                File.Move(filePath, newPath);
        }

        public static string ReplaceInvalidChars(string filePath)
        {
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            foreach (char c in invalid)
            {
                filePath = filePath.Replace(c.ToString(), "");
            }

            return filePath;
        }
        public static string UpdateFileNameForDuplicates(string outputFilePath)
        {
            var dir = Path.GetDirectoryName(outputFilePath);
            var ext = Path.GetExtension(outputFilePath);

            while (File.Exists(outputFilePath))
            {
                var newName = Path.GetFileNameWithoutExtension(outputFilePath) + " copy";
                outputFilePath = Path.Combine(dir, newName + ext);
            }

            return outputFilePath;
        }
    }
}
