using System;

namespace WpsReportTimer
{
    public static class TimerLogic
    {
        public static int GetRemainingSeconds(int durationSeconds, double elapsedSeconds)
        {
            if (durationSeconds < 0)
                throw new ArgumentOutOfRangeException("durationSeconds");
            if (elapsedSeconds < 0)
                elapsedSeconds = 0;
            return durationSeconds - (int)Math.Floor(elapsedSeconds);
        }

        public static string FormatTime(int remainingSeconds)
        {
            // Zero is deliberately shown as -0:00: it marks the start of overtime.
            bool overtime = remainingSeconds <= 0;
            int absoluteSeconds = overtime ? -remainingSeconds : remainingSeconds;
            int minutes = absoluteSeconds / 60;
            int seconds = absoluteSeconds % 60;
            return (overtime ? "-" : "") + minutes.ToString() + ":" + seconds.ToString("00");
        }

        public static bool IsOvertime(int remainingSeconds)
        {
            return remainingSeconds <= 0;
        }
    }
}
