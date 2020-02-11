using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Xml;
using System.IO;
using System.Net;


namespace GServer.MusicDL
{
    public class MBServer
    {
        private const string APIpath = @"https://musicbrainz.org/ws/2";
        private const string CoverArtPath = @"http://coverartarchive.org//release";

        public static XmlDocument Lookup(MBEntityType ent, string MBID, string[] inc = null)
        {
            // url string =  serverpath /<ENTITY>/<MBID>?inc=<INC>
            string ext = $"/{EntityStrings(ent)}/{MBID}";

            if (inc != null)
                ext = ext + $"?inc={inc[0]}";

            for (int i = 1; i < inc.Length; i++)
                ext = ext + $"%20{inc[i]}"; //multiple inc commands can be added with spaces

            return GetServerData(APIpath + ext);
        }
        public static XmlDocument Browse(MBEntityType ent, string MBID, int maxCount, int offset, string[] inc = null)
        {
            // url string =  serverpath /<ENTITY>?<ENTITY>=<MBID>&limit=<LIMIT>&offset=<OFFSET>&inc=<INC>
            string ext = $"/{EntityStrings(ent)}?{EntityStrings(ent)}={MBID}&limit={maxCount.ToString()}&offset={offset.ToString()}";

            if (inc != null)
                ext = ext + $"&inc={inc}";

            for (int i = 1; i < inc.Length; i++)
                ext = ext + $"%20{inc[i]}"; //multiple inc commands can be added with spaces

            return GetServerData(APIpath + ext);
        }
        public static XmlDocument Search(MBEntityType ent, string query, int maxCount, int offset)
        {
            // url string =  serverpath /<ENTITY>?query=<QUERY>&limit=<LIMIT>&offset=<OFFSET>
            string ext = $"/{EntityStrings(ent)}?query={query}&limit={maxCount.ToString()}&offset={offset.ToString()}";

            return GetServerData(APIpath + ext);
        }
        private static XmlDocument GetServerData(string url)
        {
            url = url.Replace(" ", "_"); //url can't have any spaces, replace with underscores

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", @"Gserver/1.0.0 ( kerr.graham.d@gmail.com )");

                var response = client.GetStringAsync(new Uri(url)).Result;

                var output = new XmlDocument();
                output.LoadXml(response);

                return output;
            }
        }


        public static String GetCoverArtDataFront(string MBID)
        {
            string url = $"{CoverArtPath}/{MBID}/front";
            return GetCoverArtLink(url);
        }
        private static String GetCoverArtLink(string url)
        {
            url = url.Replace(" ", "%20"); //url can't have any spaces, replace with underscores

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", @"Gserver/1.0.0 ( kerr.graham.d@gmail.com )");

                var response = client.GetAsync(new Uri(url)).Result;

                if (response.StatusCode.ToString() == "NotFound") //if image doesn't exist
                    return "";

                return response.RequestMessage.RequestUri.AbsoluteUri;
            }
        }
        public static byte[] GetImage(string imageUrl)
        {
            WebClient client = new WebClient();
            Stream stream = client.OpenRead(imageUrl);
            return ConvertStreamToByteArr(stream);
        }
        private static byte[] ConvertStreamToByteArr(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }


        private static string EntityStrings(MBEntityType ent)
        {
            switch (ent)
            {
                case MBEntityType.artist:
                    return "artist";
                case MBEntityType.recording:
                    return "recording";
                case MBEntityType.release:
                    return "release";
                case MBEntityType.releaseGroup:
                    return "release-group";
                default:
                    return "";
            }
        }
    }

    public enum MBEntityType
    {
        artist, recording, release, releaseGroup
    };
    public class MBEntity
    {
        public MBEntityType EntityType { get; private set; }
        public string MBID { get; protected set; } = "";
        public string Name { get; protected set; } = "";

        public MBEntity(MBEntityType EntType)
        {
            this.EntityType = EntType;
        }
    }

    public class Release : MBEntity
    {
        public string Date { get; private set; } = "";
        public string Country { get; private set; } = "";
        public string TrackCount { get; private set; } = "";
        public string SongTrackNum { get; private set; } = ""; //only exists if release is under a song

        public string CoverArtLink { get; private set; } = CoverArtLinkDefault;
        public const string CoverArtLinkDefault = "Default";
        public const string CoverArtLinkNotFound = "";

        public Release(XMLNodeG releaseNode) : base(MBEntityType.release)
        {
            if (releaseNode.node.Name == "release")
            {
                this.MBID = releaseNode.GetAttributeVal("id");
                this.Name = releaseNode.GetChild("title").InnerText;
                this.Date = releaseNode.GetChild("date").InnerText;
                this.Country = releaseNode.GetChild("country").InnerText;
                this.TrackCount = releaseNode.GetChild("medium-list").GetChild("track-count").InnerText;
                this.SongTrackNum = releaseNode.GetChild("medium-list").GetChild("medium").GetChild("track-list").GetChild("track").GetChild("number").InnerText;
            }
        }
        public void GetCoverArt()
        {
            if (this.CoverArtLink == Release.CoverArtLinkDefault) //don't need to do it twice     
                this.CoverArtLink = MBServer.GetCoverArtDataFront(this.MBID);
        }
    }
    public class ReleaseGroup : MBEntity
    {
        public string Date { get; private set; } = "";
        public string ReleaseType { get; private set; } = "";

        // ------------------- Requires Internal Search Function to Populate ----------------
        public List<Release> Releases = new List<Release>();

        public ReleaseGroup(XMLNodeG releaseGroupNode) : base(MBEntityType.releaseGroup)
        {
            if (releaseGroupNode.node.Name == "release-group")
            {
                this.MBID = releaseGroupNode.GetAttributeVal("id");
                this.Name = releaseGroupNode.GetChild("title").InnerText;
                this.Date = releaseGroupNode.GetChild("first-release-date").InnerText;
                this.ReleaseType = releaseGroupNode.GetAttributeVal("type");

                //--------------- Get releases if available ----------------
                var releaseListNode = releaseGroupNode.GetChild("release-list");

                if (releaseListNode != null)
                {
                    foreach (XMLNodeG releaseNode in releaseListNode.GetChildren("release"))
                    {
                        Releases.Add(new Release(releaseNode));
                    }
                }
            }
        }
        public void PopulateReleases()
        {
            //search the database for this artist specifically
            var doc = MBServer.Lookup(MBEntityType.releaseGroup, this.MBID, new string[] { "releases" });

            //create a new releaseGroupobject - the new function will grab the release list this time
            var releaseGroupNode = new XMLNodeG(XMLFns.GetChildren(doc.DocumentElement, "release-group").First());
            var tmpGroup = new ReleaseGroup(releaseGroupNode);

            //copy the releases from the temporary group to this one
            this.Releases = tmpGroup.Releases;
        }
    }
    public class Artist : MBEntity
    {
        // ------------------- Parameters when search was done for a song ----------------
        public string JoinPhrase { get; private set; } = "";

        // ------------------- Parameters when search was done for an artist ----------------
        public string Country { get; private set; } = "";
        public string Location { get; private set; } = "";
        public string StartDate { get; private set; } = "";

        // ------------------- Requires Internal Search Function to Populate ----------------
        public List<ReleaseGroup> ReleaseGroups = new List<ReleaseGroup>();


        public Artist(XMLNodeG artistNode) : base(MBEntityType.artist)
        {
            if (artistNode.node.Name == "name-credit") //case when artist is part of a recording query tag
            {
                this.JoinPhrase = artistNode.GetAttributeVal("joinphrase");
                artistNode = artistNode.GetChild("artist");
            }
            else if (artistNode.node.Name == "artist") //case when search was done for an artist
            {
                this.Country = artistNode.GetChild("country").InnerText;
                this.Location = artistNode.GetChild("area").GetChild("name").InnerText;
                this.StartDate = artistNode.GetChild("life-span").GetChild("begin").InnerText;
            }
            this.MBID = artistNode.GetAttributeVal("id");
            this.Name = artistNode.GetChild("name").InnerText;

            //--------------- Get releases if available ----------------
            var releaseGroupListNode = artistNode.GetChild("release-group-list");

            if (releaseGroupListNode != null)
            {
                foreach (XMLNodeG releaseGroupNode in releaseGroupListNode.GetChildren("release-group"))
                {
                    ReleaseGroups.Add(new ReleaseGroup(releaseGroupNode));
                }
            }

        }
        public void PopulateReleaseGroups()
        {
            //search the database for this artist specifically
            var doc = MBServer.Lookup(MBEntityType.artist, this.MBID, new string[] { "release-groups" });

            //create a new artist object - the new function will grab the release list this time
            var artistNode = new XMLNodeG(XMLFns.GetChildren(doc.DocumentElement, "artist").First());
            var tmpArtist = new Artist(artistNode);

            //copy the release groups from the temporary arist to this one
            this.ReleaseGroups = tmpArtist.ReleaseGroups;
        } //requires a server search

    }
    public class Song : MBEntity
    {
        public List<Artist> Artists = new List<Artist>();
        public List<Release> Releases = new List<Release>();
        private int duration = -1;

        public string ArtistsNameString
        {
            get
            {
                string output = "";

                foreach (Artist a in this.Artists)
                    output = output + a.JoinPhrase + a.Name;

                return output;
            }
        }
        public string ReleasesNameString
        {
            get
            {
                string output = "";

                foreach (Release a in this.Releases)
                    output = output + a.Name + ", ";

                return output;
            }
        }
        public string Duration
        {
            get
            {
                if (this.duration == -1)
                    return "?:??";
                else
                {
                    var totalSeconds = Math.Round(this.duration / 1000.0, 0);
                    var seconds = totalSeconds % 60; //get the number of seconds not in a minute
                    var minutes = (totalSeconds - seconds) / 60.0;
                    return $"{minutes}:{seconds}";
                }
            }
        }

        public Song(XMLNodeG recordNode) : base(MBEntityType.recording)
        {
            if (recordNode.node.Name == "recording")
            {
                this.Name = recordNode.GetChild("title").InnerText;
                this.MBID = recordNode.GetAttributeVal("id");

                //-------------- Get duration ---------------------
                string dur = recordNode.GetChild("length").InnerText;
                if (dur != "") //if duration exists
                    this.duration = int.Parse(dur);

                //-------------- Get artists ---------------------
                foreach (XMLNodeG ArtistNode in recordNode.GetChildren("artist-credit"))
                {
                    foreach (XMLNodeG ArtistNameNode in ArtistNode.GetChildren("name-credit"))
                    {
                        Artists.Add(new Artist(ArtistNameNode));
                        Artists.Reverse(); //featured artists are always shown first for some reason, make them come last
                    }
                }

                //-------------- Get releases ---------------------

                foreach (XMLNodeG releaseListNode in recordNode.GetChildren("release-list"))
                {
                    foreach (XMLNodeG releaseNode in releaseListNode.GetChildren("release"))
                    {
                        Releases.Add(new Release(releaseNode));
                    }
                }
            }
        }

        public void TagMP3File(string filePath, int releaseIndex)
        {
            var tfile = TagLib.File.Create(filePath);

            // change title in the file
            tfile.Tag.Title = this.Name;
            tfile.Tag.MusicBrainzTrackId = this.MBID;

            //change artists
            if (Artists.Count > 0)
            {
                tfile.Tag.Performers = new String[1] { this.ArtistsNameString };
                tfile.Tag.MusicBrainzReleaseArtistId = this.Artists[0].MBID;
            }

            //change release info
            if (releaseIndex < Releases.Count && releaseIndex >= 0)
            {
                var release = Releases[releaseIndex];

                tfile.Tag.Album = release.Name;
                tfile.Tag.MusicBrainzReleaseId = release.MBID;
                tfile.Tag.MusicBrainzReleaseCountry = release.Country;

                //-------------- Safely parse track num and count---------------------

                uint tmpVal = 0;

                if(uint.TryParse(release.SongTrackNum, out tmpVal))
                    tfile.Tag.Track = uint.Parse(release.SongTrackNum);

                if (uint.TryParse(release.TrackCount, out tmpVal))
                    tfile.Tag.TrackCount = uint.Parse(release.TrackCount);

                //-------------------- get the album art -------------------
                release.GetCoverArt(); //query the server for the link
                if (release.CoverArtLink != Release.CoverArtLinkNotFound)
                    MusicTagging.addPictureNoSave(tfile, MBServer.GetImage(release.CoverArtLink));
            }

            tfile.Save();
        }

    }


    public class XMLFns
    {
        public static List<XmlNode> GetChildren(XmlElement parentDoc, string childName)
        {
            var output = new List<XmlNode>();

            if (parentDoc is null)
                return output;

            foreach (XmlNode n in parentDoc.ChildNodes)
            {
                if (n.Name == childName)
                    output.Add(n);
            }

            return output;
        }
    }
    public class XMLNodeG
    {
        public XmlNode node { get; private set; }
        public string InnerText
        {
            get
            {
                if (this.node is null)
                    return "";

                return this.node.InnerText;
            }
        }


        public XMLNodeG(XmlNode node)
        {
            this.node = node;
        }

        public List<XMLNodeG> GetChildren(string childName)
        {
            var output = new List<XMLNodeG>();

            if (this.node is null)
                return output;

            foreach (XmlNode n in this.node.ChildNodes)
            {
                if (n.Name == childName)
                    output.Add(new XMLNodeG(n));
            }

            return output;
        }
        public XMLNodeG GetChild(string childName)
        {
            if (this.node is null)
                return new XMLNodeG(null);

            foreach (XmlNode n in this.node.ChildNodes)
            {
                if (n.Name == childName)
                    return new XMLNodeG(n);
            }

            return new XMLNodeG(null);
        }
        public string GetAttributeVal(string attrName)
        {
            if (this.node is null)
                return "";

            foreach (XmlAttribute attr in this.node.Attributes)
            {
                if (attr.Name == attrName)
                    return attr.Value;
            }

            return "";
        }

    }

}
