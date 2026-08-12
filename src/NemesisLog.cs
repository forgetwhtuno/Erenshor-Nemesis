namespace ErenshorNemesis
{
    // Loader-neutral logging surface so NemesisDirector (a large domain-logic class shared across
    // the plugin) does not depend on BepInEx or Lunaris logging types directly.
    internal interface INemesisLog
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    internal sealed class LunarisNemesisLog : INemesisLog
    {
        private readonly Lunaris.ILog _log;
        internal LunarisNemesisLog(Lunaris.ILog log) { _log = log; }
        public void LogInfo(string message) { if (_log != null) _log.LogInfo(message ?? string.Empty); }
        public void LogWarning(string message) { if (_log != null) _log.LogWarning(message ?? string.Empty); }
        public void LogError(string message) { if (_log != null) _log.LogError(message ?? string.Empty); }
    }
}
