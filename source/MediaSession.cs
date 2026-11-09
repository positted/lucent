using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Lucent
{
    internal sealed class MediaTrack
    {
        public string Title,Artist,Source;
        public bool Playing,CanPlay,CanPause,CanPrevious,CanNext,CanSeek,CanShuffle,CanRepeat,Shuffling;
        public int Repeat;   // 0 off, 1 this track, 2 all
        public double Position,Duration;
        public byte[] Artwork;
    }
    // The songs that have played while Lucent was running, newest last: what the "Recently played" panel shows. The media apps only tell Lucent about the song
    // playing now, never what is coming, so this is a history and not a queue. Music from a web browser (videos) is left out. Kept in memory only.
    internal sealed class HistoryEntry
    {
        public string Key,Title,Artist;
        public byte[] Artwork;
        public DateTime At;
    }
    internal static class PlayHistory
    {
        const int Limit=60;
        static readonly System.Collections.Generic.List<HistoryEntry> entries=new System.Collections.Generic.List<HistoryEntry>();
        internal static event Action Changed;
        internal static void Add(MediaTrack track)
        {
            if(track==null||string.IsNullOrWhiteSpace(track.Title)||DiscordActivity.IsBrowserSource(track.Source))return;
            string key=track.Source+"|"+track.Title+"|"+track.Artist;
            lock(entries)
            {
                if(entries.Count>0&&entries[entries.Count-1].Key==key)
                {
                    var last=entries[entries.Count-1];
                    if(last.Artwork==null&&track.Artwork!=null){last.Artwork=track.Artwork;}else return;
                }
                else
                {
                    entries.Add(new HistoryEntry {Key=key,Title=track.Title,Artist=track.Artist,Artwork=track.Artwork,At=DateTime.Now});
                    if(entries.Count>Limit)entries.RemoveAt(0);
                }
            }
            var changed=Changed;if(changed!=null)changed();
        }
        // Newest first.
        internal static System.Collections.Generic.List<HistoryEntry> Newest()
        {
            lock(entries){var copy=new System.Collections.Generic.List<HistoryEntry>(entries);copy.Reverse();return copy;}
        }
        internal static void Clear(){lock(entries)entries.Clear();var changed=Changed;if(changed!=null)changed();}
    }
    internal sealed class MediaSession
    {
        static async Task<T> Finish<T>(Windows.Foundation.IAsyncOperation<T> operation)
        {
            var info=(Windows.Foundation.IAsyncInfo)operation;
            try
            {
                var clock=System.Diagnostics.Stopwatch.StartNew();
                while(info.Status==Windows.Foundation.AsyncStatus.Started)
                {
                    if(clock.ElapsedMilliseconds>2500){info.Cancel();throw new TimeoutException("The media app did not respond.");}
                    await Task.Delay(25);
                }
                return operation.GetResults();
            }
            finally{info.Close();}
        }
        GlobalSystemMediaTransportControlsSessionManager manager;
        GlobalSystemMediaTransportControlsSession session;
        string artworkKey;
        byte[] artwork;
        MediaTrack lastTrack;
        DateTime artworkChanged,artworkRead;
        double startOffset;
        // Windows names one session "current", but the music being played can be in another app, such as a paused
        // browser tab being current while Spotify plays. The one that is playing wins.
        // Media from a web browser (a video, TikTok, YouTube) is not music from a music app, so Lucent does not see it at all: not in the player, the bar, Home,
        // the colors or Discord. Only the other apps' sessions count.
        static bool Wanted(GlobalSystemMediaTransportControlsSession candidate)
        {
            try{return !DiscordActivity.IsBrowserSource(candidate.SourceAppUserModelId);}
            catch(Exception){return false;}
        }
        GlobalSystemMediaTransportControlsSession PickSession()
        {
            var current=manager.GetCurrentSession();
            if(current!=null&&!Wanted(current))current=null;
            if(current!=null&&IsPlaying(current))return current;
            foreach(var other in manager.GetSessions())if(Wanted(other)&&IsPlaying(other))return other;
            if(current!=null)return current;
            foreach(var other in manager.GetSessions())if(Wanted(other))return other;
            return null;
        }        static bool IsPlaying(GlobalSystemMediaTransportControlsSession candidate)
        {
            try{return candidate.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;}
            catch(Exception){return false;}
        }
        public async Task<MediaTrack> Read()
        {
            if(manager==null)manager=await Finish(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
            var current=PickSession();
            if(current==null){session=null;artworkKey=null;artwork=null;lastTrack=null;return null;}
            session=current;
            var metadata=await Finish(current.TryGetMediaPropertiesAsync());
            var playback=current.GetPlaybackInfo();
            var timeline=current.GetTimelineProperties();
            startOffset=timeline.StartTime.TotalSeconds;
            string key=current.SourceAppUserModelId+"|"+metadata.Title+"|"+metadata.Artist;
            // Some media apps publish the new thumbnail after the metadata event.
            // Refresh it independently and preserve the byte array when unchanged.
            // Decoding a thumbnail every refresh is wasteful, so re-read it often only
            // just after a track change and occasionally after that.
            var now=DateTime.UtcNow;if(key!=artworkKey)artworkChanged=now;
            bool readArtwork=key!=artworkKey||now-artworkChanged<TimeSpan.FromSeconds(3)||now-artworkRead>=TimeSpan.FromSeconds(2);
            byte[] fresh=readArtwork?null:artwork;
            if(readArtwork&&metadata.Thumbnail!=null)
            {
                artworkRead=now;
                using(var stream=await Finish(metadata.Thumbnail.OpenReadAsync()))
                {
                    if(stream.Size>0&&stream.Size<=4*1024*1024)
                    {
                        using(var reader=new DataReader(stream))
                        {
                            await Finish(reader.LoadAsync((uint)stream.Size));
                            fresh=new byte[(int)stream.Size];reader.ReadBytes(fresh);
                        }
                    }
                }
            }
            var latest=await Finish(current.TryGetMediaPropertiesAsync());
            string latestKey=current.SourceAppUserModelId+"|"+latest.Title+"|"+latest.Artist;
            // The track changed during this read. Keep showing the last one until the next read, rather than "nothing".
            if(latestKey!=key||PickSession()!=current)return lastTrack;
            if(key!=artworkKey||!SameArtwork(artwork,fresh))artwork=fresh;
            artworkKey=key;
            var controls=playback.Controls;
            double position=Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds);
            if(playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)position+=Math.Max(0,(DateTimeOffset.UtcNow-timeline.LastUpdatedTime).TotalSeconds);
            return lastTrack=new MediaTrack {
                Title=metadata.Title,Artist=metadata.Artist,Source=current.SourceAppUserModelId,
                Playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlay=controls.IsPlayEnabled,CanPause=controls.IsPauseEnabled,
                CanPrevious=controls.IsPreviousEnabled,CanNext=controls.IsNextEnabled,
                CanSeek=controls.IsPlaybackPositionEnabled,
                CanShuffle=controls.IsShuffleEnabled,CanRepeat=controls.IsRepeatEnabled,
                Shuffling=playback.IsShuffleActive.GetValueOrDefault(),Repeat=RepeatMode(playback.AutoRepeatMode),
                Position=position,
                Duration=Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds),Artwork=artwork
            };
        }
        static int RepeatMode(Windows.Media.MediaPlaybackAutoRepeatMode? mode)
        {
            if(!mode.HasValue)return 0;
            return mode.Value==Windows.Media.MediaPlaybackAutoRepeatMode.Track?1:mode.Value==Windows.Media.MediaPlaybackAutoRepeatMode.List?2:0;
        }
        internal static bool SameArtwork(byte[] a,byte[] b)
        {
            if(ReferenceEquals(a,b))return true;if(a==null||b==null||a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;
        }
        public async Task<bool> Seek(double seconds)
        {
            if(session==null)return false;
            return await Finish(session.TryChangePlaybackPositionAsync((long)((startOffset+Math.Max(0,seconds))*10000000)));
        }
        public async Task<bool> SetShuffle(bool on)
        {
            if(session==null)return false;
            return await Finish(session.TryChangeShuffleActiveAsync(on));
        }
        // 0 off, 1 this track, 2 all.
        public async Task<bool> SetRepeat(int mode)
        {
            if(session==null)return false;
            return await Finish(session.TryChangeAutoRepeatModeAsync(mode==1?Windows.Media.MediaPlaybackAutoRepeatMode.Track:mode==2?Windows.Media.MediaPlaybackAutoRepeatMode.List:Windows.Media.MediaPlaybackAutoRepeatMode.None));
        }
        public async Task<bool> Command(string command)
        {
            if(session==null)return false;
            switch(command)
            {
                case "previous":return await Finish(session.TrySkipPreviousAsync());
                case "next":return await Finish(session.TrySkipNextAsync());
                default:return await Finish(session.TryTogglePlayPauseAsync());
            }
        }
    }
}
