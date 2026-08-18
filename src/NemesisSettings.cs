using System;
using Lunaris.Config;

namespace ErenshorNemesis
{
    // Loader-neutral ConfigEntry-style shim over the true (fixed-key) settings. Per-character
    // rivalry state is handled separately by NemesisStateStore, not by this typed settings class,
    // because it needs a dynamic per-character section that Lunaris typed config cannot express.
    internal sealed class NemesisConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal NemesisConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class NemesisSettings
    {
        public NemesisSettings() { }

        [Config("Enabled", "Nemesis", "Enable the persistent Nemesis social system.")]
        public bool Enabled = true;

        [Config("NotifyDeepSims", "Nemesis", "Let Deep Sims observe sanitized Nemesis events when installed.")]
        public bool NotifyDeepSims = true;

        [Config("UseLlmVoice", "Nemesis", "When Deep Sims local inference is available, allow one guarded short rivalry line; templates remain the fallback.")]
        public bool UseLlmVoice = true;

        [Config("VoiceTimeoutSeconds", "Nemesis", "Seconds to wait for an optional generated line before the template is spoken instead, clamped to 4-60.")]
        public int VoiceTimeoutSeconds = 12;

        [Config("Enabled", "Ambush", "Allow rare PvP ambush requests. PvP still validates every rule.")]
        public bool NaturalAmbushes = true;

        [Config("LevelRange", "Selection", "Candidate level range, clamped to 1-10.")]
        public int LevelRange = 3;

        [Config("MinimumPlayerLevel", "Ambush", "Natural Nemesis ambushes stay locked below this player level, clamped to 1-60. Forced testing still uses PvP's normal eligibility checks.")]
        public int MinimumAmbushLevel = 5;

        [Config("MinimumRivalryMinutes", "Ambush", "A rivalry must be at least this old before natural ambush opportunities begin, clamped to 0-720.")]
        public int MinimumRivalryMinutes = 20;

        [Config("ZoneEntryTaunts", "Cadence", "Allow an occasional rivalry line shortly after the player enters a different zone. Only the verified arrival is used; nothing that happened there is claimed.")]
        public bool ZoneTaunts = true;

        [Config("ZoneEntryChancePercent", "Cadence", "Chance an eligible zone arrival produces a line, clamped to 1-100.")]
        public int ZoneTauntChance = 25;

        [Config("ZoneEntryMinimumMinutes", "Cadence", "Minimum minutes between zone-entry lines, clamped to 5-720. Zone lines also consume the ordinary taunt window.")]
        public int ZoneTauntMinimumMinutes = 45;

        [Config("TauntMinimumMinutes", "Cadence", "Minimum minutes between social taunt opportunities.")]
        public int TauntMinimumMinutes = 18;

        [Config("TauntMaximumMinutes", "Cadence", "Maximum minutes between social taunt opportunities.")]
        public int TauntMaximumMinutes = 45;

        [Config("MinimumMinutes", "Ambush", "Minimum minutes between Nemesis ambush opportunities.")]
        public int AmbushMinimumMinutes = 35;

        [Config("MaximumMinutes", "Ambush", "Maximum minutes between Nemesis ambush opportunities.")]
        public int AmbushMaximumMinutes = 75;

        [Config("OpportunityChancePercent", "Ambush", "Chance an eligible opportunity requests PvP, clamped to 5-100.")]
        public int AmbushChance = 20;
    }
}
