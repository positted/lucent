using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace Lucent
{
    // The artist's picture beside their name in the players. Off until it is switched on in Settings, because finding it sends the
    // artist's name to Deezer's public search (the same kind of lookup lyrics make). Only the first artist of a song is looked up, a
    // picture is only used when the name that comes back is the same name, and every answer is kept for the session.
    internal static class ArtistPictures
    {
        internal static Func<bool> Enabled=()=>false;
        // Tests replace these so nothing goes online.
        internal static Func<string,string> SearchFetcher=Search;
        internal static Func<string,byte[]> ImageFetcher=Image;
        static readonly Dictionary<string,BitmapSource> known=new Dictionary<string,BitmapSource>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> asked=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly object gate=new object();
        internal static void Reset(){lock(gate){known.Clear();asked.Clear();}}

        static string Search(string name)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create("https://api.deezer.com/search/artist?limit=3&q="+Uri.EscapeDataString(name));
            request.UserAgent="Lucent";request.Timeout=8000;request.ReadWriteTimeout=8000;
            using(var response=request.GetResponse())
            using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
            {
                var text=new StringBuilder();var buffer=new char[8192];int read;
                while(text.Length<512*1024&&(read=reader.Read(buffer,0,buffer.Length))>0)text.Append(buffer,0,read);
                return text.ToString();
            }
        }
        static byte[] Image(string url)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.UserAgent="Lucent";request.Timeout=8000;request.ReadWriteTimeout=8000;
            using(var response=request.GetResponse())
            using(var stream=response.GetResponseStream())
            using(var memory=new MemoryStream())
            {
                var buffer=new byte[16384];int read;
                while(memory.Length<2*1024*1024&&(read=stream.Read(buffer,0,buffer.Length))>0)memory.Write(buffer,0,read);
                return memory.ToArray();
            }
        }
        // The picture's address from Deezer's answer: the result whose name is the artist's, and only from Deezer's own picture servers.
        internal static string PictureUrl(string json,string artist)
        {
            try
            {
                var list=(new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(json) as IDictionary<string,object>);
                object data;if(list==null||!list.TryGetValue("data",out data))return null;
                var items=data as System.Collections.IEnumerable;if(items==null)return null;
                foreach(object item in items)
                {
                    var entry=item as IDictionary<string,object>;if(entry==null)continue;
                    object name,small,medium;
                    if(!entry.TryGetValue("name",out name)||name==null||!string.Equals(name.ToString().Trim(),(artist??"").Trim(),StringComparison.OrdinalIgnoreCase))continue;
                    entry.TryGetValue("picture_medium",out medium);entry.TryGetValue("picture_small",out small);
                    string url=(medium??small??"").ToString();
                    if(AllowedHost(url))return url;
                }
            }
            catch(Exception){}
            return null;
        }
        internal static bool AllowedHost(string url)
        {
            Uri uri;
            if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https")return false;
            string host=uri.Host.ToLowerInvariant();
            return host.EndsWith(".dzcdn.net")||host.EndsWith(".deezer.com")||host=="dzcdn.net"||host=="deezer.com";
        }
        // Looks the first artist up in the background and hands the picture (or null) to done on the thread that asked.
        internal static void Load(string artistLine,System.Windows.Threading.Dispatcher dispatcher,Action<BitmapSource> done)
        {
            string name=LyricsService.FirstArtist(artistLine);
            if(string.IsNullOrWhiteSpace(name)||!Enabled()){done(null);return;}
            lock(gate)
            {
                BitmapSource picture;
                if(known.TryGetValue(name,out picture)){done(picture);return;}
                if(asked.Contains(name)){done(null);return;}
                asked.Add(name);
            }
            Task.Run(()=>
            {
                BitmapSource result=null;
                try
                {
                    string url=PictureUrl(SearchFetcher(name),name);
                    byte[] bytes=url==null?null:ImageFetcher(url);
                    if(bytes!=null&&bytes.Length>0)
                    {
                        var image=new BitmapImage();
                        image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=96;image.StreamSource=new MemoryStream(bytes);image.EndInit();image.Freeze();
                        result=image;
                    }
                }
                catch(Exception){}
                lock(gate){if(result!=null)known[name]=result;}
                try{dispatcher.BeginInvoke(new Action(()=>done(result)));}catch(Exception){}
            });
        }
    }

    // The round picture itself: hidden until a picture for the current artist has arrived.
    internal sealed class ArtistBadge:System.Windows.Controls.Border
    {
        string artist;
        public ArtistBadge(double size)
        {
            Width=Height=size;CornerRadius=new System.Windows.CornerRadius(size/2);
            VerticalAlignment=System.Windows.VerticalAlignment.Center;Visibility=System.Windows.Visibility.Collapsed;
            Margin=new System.Windows.Thickness(0,0,Math.Round(size*.38),0);
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(this,System.Windows.Media.BitmapScalingMode.HighQuality);
        }
        internal bool Shown {get{return Visibility==System.Windows.Visibility.Visible;}}
        // Shows the picture of the first artist in the line, when pictures are on and one is found.
        internal void Show(string artistLine,bool online)
        {
            artist=artistLine;
            Visibility=System.Windows.Visibility.Collapsed;
            if(!online||string.IsNullOrWhiteSpace(artistLine))return;
            ArtistPictures.Load(artistLine,Dispatcher,picture=>
            {
                if(picture==null||artist!=artistLine)return;
                Background=new System.Windows.Media.ImageBrush(picture){Stretch=System.Windows.Media.Stretch.UniformToFill};
                Visibility=System.Windows.Visibility.Visible;
            });
        }
    }
    // The cover picture of the song that is playing, for the Discord status (Discord needs an https address, so the cover from the player cannot be
    // used). Found with Deezer's public search by title and artist, only a result whose title and artist match, and only on Deezer's own servers.
    // Each answer is kept for the session.
    internal static class SongCovers
    {
        // Tests replace this so nothing goes online.
        internal static Func<string,string> SearchFetcher=Search;
        static readonly Dictionary<string,string> known=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        static readonly object gate=new object();
        internal static void Reset(){lock(gate)known.Clear();}
        static string Search(string query)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create("https://api.deezer.com/search?limit=5&q="+Uri.EscapeDataString(query));
            request.UserAgent="Lucent";request.Timeout=8000;request.ReadWriteTimeout=8000;
            using(var response=request.GetResponse())
            using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))
            {
                var text=new StringBuilder();var buffer=new char[8192];int read;
                while(text.Length<512*1024&&(read=reader.Read(buffer,0,buffer.Length))>0)text.Append(buffer,0,read);
                return text.ToString();
            }
        }
        static string Plain(string text){return LyricsService.CleanTitle(text??"").Trim();}
        // The cover's address from Deezer's answer, or null.
        internal static string CoverUrl(string json,string title,string artist)
        {
            try
            {
                var answer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.DeserializeObject(json) as IDictionary<string,object>;
                object data;if(answer==null||!answer.TryGetValue("data",out data))return null;
                var items=data as System.Collections.IEnumerable;if(items==null)return null;
                foreach(object item in items)
                {
                    var entry=item as IDictionary<string,object>;if(entry==null)continue;
                    object name,performer,album;
                    if(!entry.TryGetValue("title",out name)||name==null||!string.Equals(Plain(name.ToString()),Plain(title),StringComparison.OrdinalIgnoreCase))continue;
                    var who=entry.TryGetValue("artist",out performer)?performer as IDictionary<string,object>:null;object who2;
                    if(who==null||!who.TryGetValue("name",out who2)||who2==null||!string.Equals(who2.ToString().Trim(),LyricsService.FirstArtist(artist),StringComparison.OrdinalIgnoreCase))continue;
                    var disc=entry.TryGetValue("album",out album)?album as IDictionary<string,object>:null;if(disc==null)continue;
                    object big,medium;disc.TryGetValue("cover_big",out big);disc.TryGetValue("cover_medium",out medium);
                    string url=(big??medium??"").ToString();
                    if(ArtistPictures.AllowedHost(url))return url;
                }
            }
            catch(Exception){}
            return null;
        }
        // Looks the cover up (call it from a background thread). Null when there is none or the lookup fails.
        internal static string Find(string title,string artist)
        {
            string first=LyricsService.FirstArtist(artist);
            if(string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(first))return null;
            string key=first+"|"+Plain(title);
            lock(gate){string url;if(known.TryGetValue(key,out url))return url;}
            string found=null;
            // The exact search first, then a plain one: Deezer's exact search finds nothing for some titles (for example "Janice STFU").
            // Either way only a result with the same title and artist is used.
            try{found=CoverUrl(SearchFetcher("artist:\""+first+"\" track:\""+Plain(title)+"\""),title,artist);}catch(Exception){}
            if(found==null)try{found=CoverUrl(SearchFetcher(first+" "+Plain(title)),title,artist);}catch(Exception){}
            lock(gate)known[key]=found;
            return found;
        }
    }}
