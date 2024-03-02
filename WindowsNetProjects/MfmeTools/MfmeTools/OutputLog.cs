using MfmeTools.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools
{
    public static class OutputLog
    {
        private enum LogType
        {
            Information,
            Warning,
            Error
        }

        public static void Log(string text, bool echoToConsole = true)
        {
            WriteLog(LogType.Information, text, echoToConsole);
        }

        public static void LogWarning(string text, bool echoToConsole = true)
        {
            WriteLog(LogType.Warning, text, echoToConsole);
        }

        public static void LogError(string text, bool echoToConsole = true)
        {
            WriteLog(LogType.Error, text, echoToConsole);
        }

        private static void WriteLog(LogType logType, string text, bool echoToConsole = true)
        {
            text = GetPrefix(logType) + " " + text;

            Program.MainForm.OutputLogRichTextBox.AppendText(text + "\n", GetColor(logType));

            if(echoToConsole)
            {
                Console.WriteLine(text);
            }
        }

        private static Color GetColor(LogType logType)
        {
            switch(logType)
            {
                case LogType.Information:
                    return Color.Blue;
                case LogType.Warning:
                    return Color.OrangeRed;
                case LogType.Error:
                    return Color.DarkRed;
                default:
                    return Color.Black;
            }
        }

        private static string GetPrefix(LogType logType)
        {
            switch (logType)
            {
                case LogType.Information:
                    return "🛈 [INFO]";
                case LogType.Warning:
                    return "⚠ [WARNING]";
                case LogType.Error:
                    return "🛑 [ERROR]";
                default:
                    return "";
            }
        }

    }
}
