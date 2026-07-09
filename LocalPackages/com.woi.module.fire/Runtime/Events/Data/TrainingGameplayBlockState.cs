namespace Woi.Events.Data
{
    /// <summary>
    /// Shared gameplay block flag for result screens. Readable from PC integration without referencing training assemblies.
    /// </summary>
    public static class TrainingGameplayBlockState
    {
        static bool blocked;

        public static bool IsBlocked => blocked;

        public static void SetBlocked(bool value) => blocked = value;

        public static void Reset() => blocked = false;
    }
}
