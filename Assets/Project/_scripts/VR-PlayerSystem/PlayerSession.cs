using System;
using UnityEngine;


namespace Woi.DataHandler
{
    [Serializable]
    public class PlayerSession
    {
        public string PlayerName { get; set; }

        public int PlayerID { get; set; }

        public DateTime StartTime { get; set; }

        public bool IsActive { get; set; }

        public string FullName => $"{PlayerName}";

        public PlayerSession()
        {
            StartTime = DateTime.Now;
            IsActive = false;
        }

        public override string ToString()
        {
            return $"{FullName} (ID: {PlayerID})";
        }
    }
}

