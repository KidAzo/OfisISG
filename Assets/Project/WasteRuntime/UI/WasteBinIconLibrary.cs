using System;
using System.Collections.Generic;
using UnityEngine;

namespace Woi.WasteCollectionMode
{
    [CreateAssetMenu(fileName = "WasteBinIconLibrary", menuName = "Waste Collection/Bin Icon Library")]
    public class WasteBinIconLibrary : ScriptableObject
    {
        [SerializeField] private Texture2D headerIcon;
        [SerializeField] private IconEntry[] icons = Array.Empty<IconEntry>();

        private Dictionary<string, Texture2D> lookup;

        public Texture2D HeaderIcon => headerIcon;

        public bool TryGetIcon(string iconKey, out Texture2D icon)
        {
            BuildLookup();
            if (string.IsNullOrWhiteSpace(iconKey))
            {
                icon = null;
                return false;
            }

            return lookup.TryGetValue(iconKey, out icon);
        }

        private void BuildLookup()
        {
            if (lookup != null)
                return;

            lookup = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (icons == null)
                return;

            for (int i = 0; i < icons.Length; i++)
            {
                IconEntry entry = icons[i];
                if (string.IsNullOrWhiteSpace(entry.iconKey) || entry.texture == null)
                    continue;

                lookup[entry.iconKey] = entry.texture;
            }
        }

        [Serializable]
        public struct IconEntry
        {
            public string iconKey;
            public Texture2D texture;
        }
    }
}
