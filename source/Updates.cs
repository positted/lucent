using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Lucent
{
    internal sealed class UpdateInfo
    {
        public Version Latest;
        public string Tag,Url,Notes,AssetUrl,AssetName,AssetDigest;
        public long AssetSize;
    }

    // Finds out whether a newer Lucent has been published. The only thing sent is an ordinary web request for a small public page
    // (a GitHub release or a version file), with no identifier of the user, the computer or the settings.
    internal static class Updates
    {
        // Where releases are announced: the project's GitHub repository. It is fixed in the program, so Lucent only ever checks (and sends
        // people to) this one place. Tests replace Source; nothing in the settings can.
        public const string DefaultSource="getlucent/lucent";
        // Where the project lived before it moved to the getlucent organization. GitHub sends the old address on to the new one, so pages and
        // downloads under either are the project's own.
        internal const string FormerSource="positted/lucent";
        static System.Collections.Generic.IEnumerable<string> Homes(){yield return Source;if(string.Equals(Source,DefaultSource,StringComparison.OrdinalIgnoreCase))yield return FormerSource;}
        internal static string Source=DefaultSource;

        public static Version Current{get{return typeof(Updates).Assembly.GetName().Version;}}

        // Release names are 1.MMYY: the month and the year of the release, so November 2026 is 1.1126 and February 2027 is 1.0227.
        // A second build in the same month is 1.1126.2, the third 1.1126.3, and so on up to .99.
        internal static string ReleaseName(DateTime date,int build)
        {
            return "1."+date.Month.ToString("00")+(date.Year%100).ToString("00")+(build>1?"."+Math.Min(99,build):"");
        }
        // How a version is written for people. Builds before the month-and-year names keep their three numbers.
        internal static string Show(Version version)
        {
            if(version.Major==0)return version.ToString(3);
            return version.Major+"."+version.Minor.ToString("0000")+(version.Build>0?"."+version.Build:"");
        }
        // Which of two versions is newer. In 1.MMYY the year counts before the month, so 1.0127 (January 2027) is newer than 1.1226 (December 2026).
        internal static int Compare(Version first,Version second)
        {
            Func<Version,int[]> key=v=>v.Major==0?new[]{0,v.Minor,v.Build,v.Revision}:new[]{v.Major,v.Minor%100,v.Minor/100,v.Build};
            var a=key(first);var b=key(second);
            for(int i=0;i<4;i++)if(a[i]!=b[i])return a[i]<b[i]?-1:1;
            return 0;
        }

        // 0.6.27, v0.7 and "Lucent 1.0.2 (beta)" all read as versions; anything without a number does not.
        internal static Version ParseVersion(string text)
        {
            if(string.IsNullOrWhiteSpace(text))return null;
            var m=Regex.Match(text,@"(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?");
            if(!m.Success)return null;
            Func<int,int> part=i=>m.Groups[i].Success?Math.Min(int.Parse(m.Groups[i].Value),int.MaxValue/2):0;
            try{return new Version(part(1),part(2),part(3),part(4));}
            catch(Exception){return null;}
        }
        // The address to ask. A GitHub "owner/repo" becomes its latest-release page; a plain https address is used as it is; nothing else is allowed.
        internal static string FeedUrl(string source)
        {
            source=(source??"").Trim();
            if(Regex.IsMatch(source,@"^[A-Za-z0-9_.-]{1,60}/[A-Za-z0-9_.-]{1,100}$"))return "https://api.github.com/repos/"+source+"/releases/latest";
            if(source.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&Uri.IsWellFormedUriString(source,UriKind.Absolute))return source;
            return null;
        }
        // Reads either a GitHub release or a small file like {"version":"0.7.0","url":"https://...","notes":"..."}.
        internal static UpdateInfo Parse(string json)
        {
            IDictionary<string,object> data;
            try{data=new JavaScriptSerializer{MaxJsonLength=512*1024}.DeserializeObject(json) as IDictionary<string,object>;}
            catch(Exception){return null;}
            if(data==null)return null;
            Func<string,string> text=key=>{object value;return data.TryGetValue(key,out value)&&value!=null?value.ToString():null;};
            Func<string,bool> flag=key=>{object value;return data.TryGetValue(key,out value)&&value is bool&&(bool)value;};
            // A draft or pre-release is not something to tell people to install.
            if(flag("draft")||flag("prerelease"))return null;
            string tag=text("tag_name")??text("version");
            var version=ParseVersion(tag);
            if(version==null)return null;
            string notes=text("body")??text("notes")??"";
            if(notes.Length>600)notes=notes.Substring(0,600).TrimEnd()+"…";
            var info=new UpdateInfo {Latest=version,Tag=tag.Trim(),Url=text("html_url")??text("url"),Notes=notes.Replace("\r","").Trim()};
            // The setup file attached to the release, if there is one.
            object assets;
            if(data.TryGetValue("assets",out assets)&&assets is System.Collections.IEnumerable&&!(assets is string))
                foreach(object item in (System.Collections.IEnumerable)assets)
                {
                    var asset=item as IDictionary<string,object>;if(asset==null)continue;
                    Func<string,string> field=key=>{object value;return asset.TryGetValue(key,out value)&&value!=null?value.ToString():null;};
                    string name=field("name")??"";
                    if(!Regex.IsMatch(name,@"^Lucent-Setup(-[A-Za-z0-9._-]+)?\.exe$",RegexOptions.IgnoreCase))continue;
                    info.AssetName=name;info.AssetUrl=field("browser_download_url");info.AssetDigest=field("digest");
                    long size;if(long.TryParse(field("size"),out size))info.AssetSize=size;
                    break;
                }
            return info;
        }
        internal static bool IsNewer(UpdateInfo info,Version current){return info!=null&&info.Latest!=null&&Compare(info.Latest,current)>0;}
        // Only a secure web address is ever opened from an update notice.
        internal static bool CanOpen(string url){return !string.IsNullOrEmpty(url)&&url.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&Uri.IsWellFormedUriString(url,UriKind.Absolute);}

        // The release page to open: when updates come from a GitHub repository, only a page of that same repository is opened.
        internal static bool CanOpenRelease(string url)
        {
            if(!CanOpen(url))return false;
            if(!Regex.IsMatch(Source??"",@"^[A-Za-z0-9_.-]{1,60}/[A-Za-z0-9_.-]{1,100}$"))return true;
            foreach(string home in Homes())if(url.StartsWith("https://github.com/"+home+"/",StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }

        // The setup file may only come from this project's own releases, over https.
        internal static bool CanDownload(string url)
        {
            Uri uri;
            if(!CanOpen(url)||!Uri.TryCreate(url,UriKind.Absolute,out uri))return false;
            if(!Regex.IsMatch(Source??"",@"^[A-Za-z0-9_.-]{1,60}/[A-Za-z0-9_.-]{1,100}$"))return false;
            if(!uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase))return false;
            foreach(string home in Homes())if(uri.AbsolutePath.StartsWith("/"+home+"/releases/download/",StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
        // Saves the release's setup file. Tests replace FileFetcher so nothing is downloaded.
        internal static Action<UpdateInfo,string,Action<long,long>> FileFetcher=SaveSetup;
        public static void Save(UpdateInfo info,string path,Action<long,long> progress)
        {
            if(info==null||!CanDownload(info.AssetUrl))throw new InvalidOperationException("This release has no setup file Lucent can download.");
            FileFetcher(info,path,progress);
        }
        static void SaveSetup(UpdateInfo info,string path,Action<long,long> progress)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(info.AssetUrl);
            request.UserAgent="Lucent update";request.Timeout=15000;request.ReadWriteTimeout=15000;request.AllowAutoRedirect=true;
            const long Limit=200L*1024*1024;
            try
            {
                using(var response=request.GetResponse())
                using(var from=response.GetResponseStream())
                using(var to=File.Create(path))
                {
                    long total=response.ContentLength>0?response.ContentLength:info.AssetSize,done=0;
                    if(total>Limit)throw new InvalidOperationException("The update file is larger than expected, so it was not downloaded.");
                    var buffer=new byte[64*1024];int read;
                    while((read=from.Read(buffer,0,buffer.Length))>0)
                    {
                        done+=read;
                        if(done>Limit)throw new InvalidOperationException("The update file is larger than expected, so it was not downloaded.");
                        to.Write(buffer,0,read);if(progress!=null)progress(done,total);
                    }
                }
                VerifyDigest(info,path);
            }
            catch(Exception){try{if(File.Exists(path))File.Delete(path);}catch(IOException){}throw;}
        }
        // GitHub publishes a SHA-256 for each file; when it is there, the download must match it.
        internal static void VerifyDigest(UpdateInfo info,string path)
        {
            string digest=info.AssetDigest??"";
            if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase))return;
            string actual;
            using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(path))actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");
            if(!string.Equals(actual,digest.Substring(7),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The downloaded file does not match the one published, so it was not used.");
        }

        // Tests replace this so no request is ever made.
        internal static Func<string,string> Fetcher=Download;
        public static UpdateInfo Check(string source)
        {
            string url=FeedUrl(source);
            if(url==null)throw new InvalidOperationException("That is not an address Lucent can check. Use owner/repository, or an https address.");
            string answer;
            try{answer=Fetcher(url);}
            catch(WebException error)
            {
                var reply=error.Response as HttpWebResponse;
                // A repository with no release yet answers "not found" for its latest release.
                if(reply!=null&&reply.StatusCode==HttpStatusCode.NotFound)throw new InvalidOperationException("No release has been published yet.");
                throw;
            }
            var info=Parse(answer);
            if(info==null)throw new InvalidOperationException("The update page did not say which version is newest.");
            return info;
        }
        static string Download(string url)
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.UserAgent="Lucent update check";
            request.Accept="application/vnd.github+json, application/json";
            request.Timeout=8000;request.ReadWriteTimeout=8000;request.AllowAutoRedirect=true;
            using(var response=request.GetResponse())
            using(var stream=response.GetResponseStream())
            using(var reader=new StreamReader(stream,System.Text.Encoding.UTF8))
            {
                // The whole answer, up to a limit (one Read call returns only what has arrived so far, which cut a release's details short).
                var text=new System.Text.StringBuilder();var buffer=new char[8192];int read;
                while(text.Length<512*1024&&(read=reader.Read(buffer,0,buffer.Length))>0)text.Append(buffer,0,read);
                return text.ToString();
            }
        }
    }
}
