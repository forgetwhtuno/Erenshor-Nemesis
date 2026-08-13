namespace ErenshorNemesis
{
    public sealed class NemesisControlState
    {
        public bool GameplayReady;
        public bool HasNemesis;
        public string NemesisName;
        public int GrudgePoints;
        public string Record;
        public string[] CandidateNames;
        public bool HasPendingConfirmation;
        public string PendingConfirmation;
        public string Status;
        public bool Enabled;
        public bool NaturalAmbushes;
        public bool ZoneTaunts;
        public bool NotifyDeepSims;
        public bool UseLlmVoice;
    }

    public static class NemesisControlApi
    {
        public const int ApiVersion = 1;
        public const string ModuleId = "nemesis";
        public static bool HasDedicatedPanel { get { return false; } }
        public static bool IsPanelOpen { get { return false; } }

        public static NemesisControlState GetBasicState()
        {
            return new NemesisControlState
            {
                GameplayReady = NemesisDirector.ControlReady(),
                HasNemesis = NemesisDirector.ControlHasNemesis(),
                NemesisName = NemesisDirector.ControlNemesisName(),
                GrudgePoints = NemesisDirector.ControlGrudgePoints(),
                Record = NemesisDirector.ControlRecordText(),
                CandidateNames = NemesisDirector.ControlCandidateNames(),
                HasPendingConfirmation = NemesisDirector.ControlHasPendingChange(),
                PendingConfirmation = NemesisDirector.ControlPendingChangeText(),
                Status = NemesisDirector.ControlStatus(),
                Enabled = NemesisDirector.ControlEnabled(),
                NaturalAmbushes = NemesisDirector.ControlNaturalAmbushes(),
                ZoneTaunts = NemesisDirector.ControlZoneTaunts(),
                NotifyDeepSims = NemesisDirector.ControlNotifyDeepSims(),
                UseLlmVoice = NemesisDirector.ControlUseLlmVoice()
            };
        }
        public static string GetStatus()
        {
            NemesisControlState s = GetBasicState();
            return NemesisHubPresentation.Build(s.Enabled, s.HasNemesis, s.NemesisName, s.GrudgePoints, s.Record, s.HasPendingConfirmation, s.CandidateNames == null ? 0 : s.CandidateNames.Length);
        }
        public static bool SetEnabled(bool value) { return SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlSetEnabled(value); }
        public static bool SetNaturalAmbushes(bool value) { return SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlSetNaturalAmbushes(value); }
        public static bool SetZoneTaunts(bool value) { return SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlSetZoneTaunts(value); }
        public static bool SetNotifyDeepSims(bool value) { return NemesisDirector.ControlSetNotifyDeepSims(value); }
        public static bool SetUseLlmVoice(bool value) { return NemesisDirector.ControlSetUseLlmVoice(value); }

        // Every mutating entry point additionally requires SuiteUiPolicy.IsGameplayReady() (the
        // canonical 1.0s-stable + CanMove-latch gate) on top of NemesisDirector's own lighter-weight
        // Ready() check, so Hub/Aura-originated requests can never land mid character-select/zoning/
        // load even for a single frame. This does not change NemesisDirector's existing confirmation
        // semantics - it only decides whether the request is accepted into the pending-action queue.
        public static bool TrySelect(string simName)
        {
            ErenshorNemesisPlugin p = ErenshorNemesisPlugin.Instance;
            return p != null && SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlReady() && p.RequestControlSelect(simName);
        }
        public static bool TryClear()
        {
            ErenshorNemesisPlugin p = ErenshorNemesisPlugin.Instance;
            return p != null && SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlHasNemesis() && p.RequestControlClear();
        }
        public static bool TryConfirmPending()
        {
            ErenshorNemesisPlugin p = ErenshorNemesisPlugin.Instance;
            return p != null && SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlHasPendingChange() && p.RequestControlConfirm();
        }
        public static bool TryCancelPending()
        {
            ErenshorNemesisPlugin p = ErenshorNemesisPlugin.Instance;
            return p != null && SuiteUiPolicy.IsGameplayReady() && NemesisDirector.ControlHasPendingChange() && p.RequestControlCancelPending();
        }
        public static bool OpenPanel() { return false; }
        public static bool ClosePanel() { return false; }
    }
}
