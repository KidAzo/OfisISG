using UnityEngine;

namespace WoiUtils.AudioSystem
{
    public struct PlayContext
    {
        public bool ignoreCooldowns;
        public bool hasPosition;
        public Vector3 position;

        public bool hasFollow;
        public Transform follow;

        public float volumeMul; // default 1
        public float pitchMul;  // default 1

        public bool hasClipIndex;
        public int clipIndex;

        public bool queued;

        public static PlayContext Default => new PlayContext
        {
            hasPosition = false,
            position = default,

            hasFollow = false,
            follow = null,

            volumeMul = 1f,
            pitchMul = 1f,

            hasClipIndex = false,
            clipIndex = -1,

            queued = false
        };

        public static PlayContext DebugNoCooldown()
        {
            var c = Default;
            c.ignoreCooldowns = true;
            return c;
        }

        public static PlayContext At(Vector3 pos)
        {
            var ctx = Default;
            ctx.hasPosition = true;
            ctx.position = pos;

            // If position is set, follow should be off by default
            ctx.hasFollow = false;
            ctx.follow = null;

            return ctx;
        }

        public static PlayContext Follow(Transform t)
        {
            var ctx = Default;
            ctx.hasFollow = t != null;
            ctx.follow = t;

            // If follow is set, position should be off by default
            ctx.hasPosition = false;
            ctx.position = default;

            return ctx;
        }

        public static PlayContext WithClipIndex(int index, bool ignoreCooldowns = false)
        {
            var ctx = Default;
            ctx.hasClipIndex = true;
            ctx.ignoreCooldowns = ignoreCooldowns;
            ctx.clipIndex = index;
            return ctx;
        }

        public static PlayContext WithVolumePitch(float volumeMul, float pitchMul)
        {
            var ctx = Default;
            ctx.volumeMul = volumeMul <= 0f ? 1f : volumeMul;
            ctx.pitchMul = pitchMul <= 0f ? 1f : pitchMul;
            return ctx;
        }

        // -------- Fluent modifiers (important for AudioTrigger without losing inspector data) --------
        public PlayContext SetClipIndex(int index)
        {
            hasClipIndex = true;
            clipIndex = index;
            return this;
        }

        public PlayContext ClearClipIndex()
        {
            hasClipIndex = false;
            clipIndex = -1;
            return this;
        }

        public PlayContext SetPosition(Vector3 pos)
        {
            hasPosition = true;
            position = pos;

            hasFollow = false;
            follow = null;

            return this;
        }

        public PlayContext SetFollow(Transform t)
        {
            hasFollow = t != null;
            follow = t;

            hasPosition = false;
            position = default;

            return this;
        }

        public PlayContext SetVolumePitch(float volMul, float pitMul)
        {
            volumeMul = volMul <= 0f ? 1f : volMul;
            pitchMul = pitMul <= 0f ? 1f : pitMul;
            return this;
        }
    }
}
