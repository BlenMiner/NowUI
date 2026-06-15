using UnityEngine;

namespace NowUI
{
    internal static class NowTime
    {
        public static double realtimeSinceStartup => Time.realtimeSinceStartupAsDouble;
    }
}
