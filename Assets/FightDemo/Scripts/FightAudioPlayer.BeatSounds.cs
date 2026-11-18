using FMODUnity;
namespace RhythmHunter.FightDemo
{
    public sealed partial class FightAudioPlayer
    {
        private FightBeatSoundScheduler beatSounds;
        private string beatSoundPath;
        public string BeatSoundStatus => beatSounds?.LastResult ?? "Not prepared";
        public int ScheduledBeatSounds => beatSounds?.ScheduledCount ?? 0;
        public int ImmediateBeatSounds => beatSounds?.ImmediateCount ?? 0;
        public void PrepareBeatSounds(string path)
        {
            if(beatSounds!=null && beatSoundPath==path)return;
            StopBeatSounds();beatSoundPath=path;
            if(!string.IsNullOrWhiteSpace(path))beatSounds=new FightBeatSoundScheduler(RuntimeManager.StudioSystem,path);
        }
        public bool PlayBeatSound(string path,float volume,double targetMs,bool align,float minimumLeadMs)
        {
            if(!isActiveAndEnabled || !soundEnabled || fight==null || fight.BattleEnded || Paused(false) || volume<=0)return false;
            var clock=fight.BeatClock;
            if(!clock || clock.IsPaused || !clock.TryGetTimelinePositionMs(out int now))return false;
            PrepareBeatSounds(path);
            return beatSounds!=null && beatSounds.PlayAt(targetMs,now,clock.MusicPitch,volume*masterVolume,align,minimumLeadMs);
        }
        private void UpdateBeatSounds()
        {
            if(beatSounds==null || !fight || !fight.BeatClock)return;
            var clock=fight.BeatClock;
            if(clock.TryGetTimelinePositionMs(out int now))
                beatSounds.Tick(now,clock.MusicPitch,Paused(false)||clock.IsPaused||clock.BossSectionJumpInProgress);
        }
        public void StopBeatSounds(){beatSounds?.Dispose();beatSounds=null;beatSoundPath=null;}
    }
}
