using UnityEngine;

namespace WoiUtils.AudioSystem
{
    public partial struct PlayContext
    {
        public bool ignoreCooldowns;

        /// <summary>
        /// When true: (1) skips <see cref="InstanceMode.SinglePerCategory"/> pre-play category cull.
        /// (2) When the voice pool is full, only steals voices whose <see cref="SoundDefinition"/> uses the same
        /// <b>custom</b> category key as this sound. If this sound does not use a custom category, no pool steal runs
        /// (avoids killing unrelated enum-category SFX). Prefer one shared custom key on start/loop/end assets.
        /// </summary>
        public bool suppressSameCategorySteal;

        /// <summary>
        /// When true, <see cref="AudioSystem.Play"/> routes to immediate clip resolution instead of queue-based routing
        /// (e.g. <see cref="ClipSelectionMode.QueueAll"/> would otherwise return no voice).
        /// </summary>
        public bool forceImmediatePlay;

        public bool hasPosition;
        public Vector3 position;

        public bool hasFollow;
        public Transform follow;

        public float volumeMul; // default 1
        public float pitchMul;  // default 1

        public bool hasClipIndex;
        public int clipIndex;

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

            suppressSameCategorySteal = false,
            forceImmediatePlay = false,
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
        public PlayContext SetSuppressSameCategorySteal(bool suppress = true)
        {
            suppressSameCategorySteal = suppress;
            return this;
        }

        public PlayContext SetForceImmediatePlay(bool force = true)
        {
            forceImmediatePlay = force;
            return this;
        }

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
            volumeMul = volMul < 0f ? 1f : volMul;
            pitchMul = pitMul < 0f ? 1f : pitMul;
            return this;
        }
    }
}
