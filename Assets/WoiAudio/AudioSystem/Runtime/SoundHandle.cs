namespace WoiUtils.AudioSystem
{
    public struct SoundHandle
    {
        internal int id;
        internal int generation;
        internal AudioVoice voice;

        public bool IsValid => voice != null;
    }
}