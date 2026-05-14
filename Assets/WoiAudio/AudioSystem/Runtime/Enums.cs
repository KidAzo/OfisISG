namespace WoiUtils.AudioSystem
{
    public enum AudioCategory { SFX = 0, UI = 1, VO = 2, Music = 3, Ambience = 4 }

    public enum ClipSelectionMode { Single = 0, RandomWeighted = 1, Sequence = 2, QueueAll = 3 }

    public enum DelayMode { None = 0, Fixed = 1,  RandomRange = 2 }

    /// <summary>
    /// <see cref="Multiple"/>: overlapping voices allowed.<br/>
    /// <see cref="SingleGlobal"/>: one voice per <see cref="SoundDefinition"/> asset (same asset restarts).<br/>
    /// <see cref="SinglePerCategory"/>: before playing, stops every active voice whose category matches this sound (different SoundDefinitions with same UI/SFX/etc. replace each other).
    /// </summary>
    public enum InstanceMode { Multiple = 0, SingleGlobal = 1, SinglePerCategory = 2 }

    public enum ReTriggerMode { Restart = 0, Ignore = 1 }
  
    public enum ScheduleMode { Immediate = 0, Queue = 1 }
  
    public enum QueueScope { PerSound = 0, PerCategory = 1 }
}
