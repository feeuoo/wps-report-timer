using System;

namespace WpsReportTimer
{
    internal static class TimerLogicTests
    {
        private static int failures;

        private static void Check(string name, string actual, string expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("PASS " + name + " = " + actual);
            }
            else
            {
                failures++;
                Console.WriteLine("FAIL " + name + ": expected " + expected + ", actual " + actual);
            }
        }

        private static void Check(string name, int actual, int expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("PASS " + name + " = " + actual);
            }
            else
            {
                failures++;
                Console.WriteLine("FAIL " + name + ": expected " + expected + ", actual " + actual);
            }
        }

        public static int Main()
        {
            Check("初始 10 分钟", TimerLogic.FormatTime(TimerLogic.GetRemainingSeconds(600, 0)), "10:00");
            Check("倒数 9:59", TimerLogic.FormatTime(TimerLogic.GetRemainingSeconds(600, 1)), "9:59");
            Check("零点进入超时", TimerLogic.FormatTime(TimerLogic.GetRemainingSeconds(600, 600)), "-0:00");
            Check("超时 1 秒", TimerLogic.FormatTime(TimerLogic.GetRemainingSeconds(600, 601)), "-0:01");
            Check("超时 59 秒", TimerLogic.FormatTime(TimerLogic.GetRemainingSeconds(600, 659)), "-0:59");
            Check("超时 1 分钟", TimerLogic.FormatTime(TimerLogic.GetRemainingSeconds(600, 660)), "-1:00");
            Check("剩余秒数", TimerLogic.GetRemainingSeconds(3, 2.9), 1);
            Check("超时秒数", TimerLogic.GetRemainingSeconds(3, 3.0), 0);
            Check("超时判断", TimerLogic.IsOvertime(0).ToString(), true.ToString());
            Check("非超时判断", TimerLogic.IsOvertime(1).ToString(), false.ToString());

            Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
    }
}
