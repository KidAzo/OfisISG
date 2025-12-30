using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.Shared
{
    public static class FloatHelper 
    {
        public static float SnapToInt(this float value, float epsilon = 0.01f)
        {
            float rounded = Mathf.Round(value);

            if (Mathf.Abs(value - rounded) <= epsilon)
                return rounded;

            return value;

        }

        public static float FloorToPowerOfTwo(this float value)
        {
            if (value < 1) 
                return 1f;

            int valueInt = (int)value;
            int p = 1;

            while (p <= valueInt)
            {
                p <<= 1; 
            }

            return p >> 1;
        }
    }
}
