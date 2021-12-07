using System;
using System.Diagnostics;
using MelonLoader;

namespace VRCFaceTracking
{
    public static class Logger
    {
        private static MelonLogger.Instance _logger = new MelonLogger.Instance("VRCFaceTracking");

        public static void Debug(object obj)
        {
            StackFrame frame = new StackFrame(1);
            _logger.Msg($"[{frame.GetMethod().DeclaringType?.Name}] (DEBUG): {obj}");
        }

        public static void Msg(object obj) => _logger.Msg(obj);

        public static void Warning(object obj) => _logger.Warning(obj);

        public static void Error(object obj, Exception e = null, bool ShowStackFrame = true)
        {
            string msg = String.Empty;
            if (ShowStackFrame)
            {
                StackFrame frame = new StackFrame(1);
                msg = $"[{frame.GetMethod().DeclaringType?.Name}] ";
            }
            msg = msg + obj;
            if (e != null)
                msg = $"{msg} | Exception: {e}";
            _logger.Error(msg);
        }
    }
}