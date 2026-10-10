using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    /// <summary>
    /// Freezes player movement speed and mouse sensitivity fields to zero, matching Office Fire result screen behaviour.
    /// </summary>
    public sealed class PlayerMovementLookFreeze
    {
        private static readonly string[] MovementSpeedFieldNames =
        {
            "_walkSpeed",
            "walkSpeed",
            "_sprintSpeed",
            "sprintSpeed",
        };

        private static readonly string[] MouseSensitivityFieldNames =
        {
            "_mouseSensitivity",
            "mouseSensitivity",
        };

        private static readonly BindingFlags BehaviourFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class FloatFieldSnapshot
        {
            public Behaviour Target;
            public FieldInfo Field;
            public float OriginalValue;
        }

        private readonly List<FloatFieldSnapshot> frozenFloatFields = new();

        public bool IsFrozen { get; private set; }

        public void Freeze(Transform playerRoot)
        {
            if (playerRoot == null || IsFrozen)
                return;

            MonoBehaviour[] behaviours = playerRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                FreezeFloatFields(behaviour, MovementSpeedFieldNames);
                FreezeFloatFields(behaviour, MouseSensitivityFieldNames);
            }

            IsFrozen = true;
        }

        public void Restore()
        {
            if (!IsFrozen)
                return;

            for (int i = 0; i < frozenFloatFields.Count; i++)
            {
                FloatFieldSnapshot snapshot = frozenFloatFields[i];
                if (snapshot.Target == null || snapshot.Field == null)
                    continue;

                snapshot.Field.SetValue(snapshot.Target, snapshot.OriginalValue);
            }

            frozenFloatFields.Clear();
            IsFrozen = false;
        }

        private void FreezeFloatFields(MonoBehaviour behaviour, string[] fieldNames)
        {
            System.Type type = behaviour.GetType();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                FieldInfo field = type.GetField(fieldNames[i], BehaviourFieldFlags);
                if (field == null || field.FieldType != typeof(float))
                    continue;

                if (TryFindExistingSnapshot(behaviour, field, out _))
                {
                    field.SetValue(behaviour, 0f);
                    continue;
                }

                float originalValue = (float)field.GetValue(behaviour);
                frozenFloatFields.Add(new FloatFieldSnapshot
                {
                    Target = behaviour,
                    Field = field,
                    OriginalValue = originalValue,
                });
                field.SetValue(behaviour, 0f);
            }
        }

        private bool TryFindExistingSnapshot(Behaviour target, FieldInfo field, out FloatFieldSnapshot snapshot)
        {
            for (int i = 0; i < frozenFloatFields.Count; i++)
            {
                FloatFieldSnapshot candidate = frozenFloatFields[i];
                if (candidate.Target == target && candidate.Field == field)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = null;
            return false;
        }
    }
}
