using System;
using System.Linq;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorNemesis
{
    // Thin, optional transport adapter over the public NemesisControlApi. No gameplay logic here -
    // every action call revalidates through the owning mod's real state (NemesisControlApi, which
    // itself defers into NemesisDirector's existing select/confirm/cancel state machine via the
    // plugin's pending-request Update path). Never references ErenshorSuiteHub.dll.
    internal sealed class NemesisSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite.nemesis.v1.";

        private IAuraProvider<string> _describe;
        private IAuraProvider<string> _basicSettings;
        private IAuraProvider<string> _advancedSettings;
        private IAuraProvider<string, string, string> _settingSet;
        private IAuraProvider<string, string, string> _action;

        internal NemesisSuiteAuraProvider(LunarisPlugin owner)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            try
            {
                _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
                _describe.RegisterFunc(Describe);

                _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic"); _basicSettings.RegisterFunc(BasicSettings);
                _advancedSettings = owner.IPCAuraProvider<string>(Prefix + "settings.advanced"); _advancedSettings.RegisterFunc(AdvancedSettings);
                _settingSet = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set"); _settingSet.RegisterFunc(SetSetting);
                _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
                _action.RegisterFunc(InvokeAction);
            }
            catch
            {
                Unregister();
                throw;
            }
        }

        // Call from the plugin's OnDestroy(). MANDATORY - explicitly unregister every handler.
        internal void Unregister()
        {
            try { if (_describe != null) _describe.UnregisterFunc(); } catch { } _describe = null;
            try { if (_basicSettings != null) _basicSettings.UnregisterFunc(); } catch { } _basicSettings = null;
            try { if (_advancedSettings != null) _advancedSettings.UnregisterFunc(); } catch { } _advancedSettings = null;
            try { if (_settingSet != null) _settingSet.UnregisterFunc(); } catch { } _settingSet = null;
            try { if (_action != null) _action.UnregisterFunc(); } catch { } _action = null;
        }

        private string Describe()
        {
            NemesisControlState state = NemesisControlApi.GetBasicState();
            string status = NemesisHubPresentation.Build(state.Enabled, state.HasNemesis, state.NemesisName,
                state.GrudgePoints, state.Record, state.HasPendingConfirmation,
                state.AutomaticCandidateNames == null ? 0 : state.AutomaticCandidateNames.Length);
            return "protocol=1"
                + "&module=" + NemesisControlApi.ModuleId
                + "&display=" + Uri.EscapeDataString("Nemesis")
                + "&version=" + Uri.EscapeDataString(ErenshorNemesisPlugin.PluginVersion)
                + "&status=" + Uri.EscapeDataString(status)
                + "&candidateCount=" + ((state.AutomaticCandidateNames ?? new string[0]).Length)
                + "&explicitCandidateCount=" + ((state.CandidateNames ?? new string[0]).Length)
                + "&actions=openPanel,closePanel,select,clear,confirm,cancel";
        }

        private string BasicSettings()
        {
            NemesisControlState s = NemesisControlApi.GetBasicState();
            return BoolLine("enabled", "Nemesis Enabled", "basic", s.Enabled) + "\n" +
                   BoolLine("naturalAmbushes", "Natural ambush requests", "basic", s.NaturalAmbushes) + "\n" +
                   BoolLine("zoneTaunts", "Zone-entry rivalry lines", "basic", s.ZoneTaunts);
        }

        private string AdvancedSettings()
        {
            NemesisControlState s = NemesisControlApi.GetBasicState();
            return BoolLine("notifyDeepSims", "Notify Deep Sims", "advanced", s.NotifyDeepSims) + "\n" +
                   BoolLine("llmVoice", "Optional LLM rivalry voice", "advanced", s.UseLlmVoice);
        }

        private string SetSetting(string id, string value)
        {
            bool v = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            if (id == "enabled") return NemesisControlApi.SetEnabled(v) ? "ok" : "rejected";
            if (id == "naturalAmbushes") return NemesisControlApi.SetNaturalAmbushes(v) ? "ok" : "rejected";
            if (id == "zoneTaunts") return NemesisControlApi.SetZoneTaunts(v) ? "ok" : "rejected";
            if (id == "notifyDeepSims") return NemesisControlApi.SetNotifyDeepSims(v) ? "ok" : "rejected";
            if (id == "llmVoice") return NemesisControlApi.SetUseLlmVoice(v) ? "ok" : "rejected";
            return "unknown setting";
        }

        private static string BoolLine(string id, string label, string tier, bool value)
        {
            return "id=" + Uri.EscapeDataString(id) + "&label=" + Uri.EscapeDataString(label) + "&tier=" + tier + "&type=bool&value=" + (value ? "true" : "false") + "&mutable=true";
        }

        private string InvokeAction(string actionId, string argument)
        {
            // Only advertised, explicit, safe player-facing actions. Revalidate everything - Hub is
            // not authorization. These are thin call-throughs into NemesisControlApi.Try*, which
            // themselves only defer a bounded request onto NemesisDirector's existing
            // select/confirm/cancel state machine on the plugin's Update tick; the confirmation
            // semantics (established rivalry -> pending + confirm/cancel, unestablished -> immediate)
            // are unchanged here.
            switch (actionId)
            {
                case "openPanel": return NemesisControlApi.OpenPanel() ? "ok" : "rejected";
                case "closePanel": return NemesisControlApi.ClosePanel() ? "ok" : "rejected";
                case "select":
                {
                    string requested = (argument ?? string.Empty).Trim();
                    if (requested.Length == 0) return "rejected: missing candidate name";
                    // Revalidate against the explicit-selection list before queuing. This
                    // deliberately allows a native Friend selected by name, while automatic/random
                    // selection uses the stricter automatic list.
                    // NemesisDirector.Select() would also refuse an
                    // ineligible name, but we reject early here so the caller gets a clear result
                    // instead of a silently-dropped request.
                    string[] candidates = NemesisControlApi.GetBasicState().CandidateNames ?? Array.Empty<string>();
                    bool eligible = candidates.Any(c => string.Equals(c, requested, StringComparison.OrdinalIgnoreCase));
                    if (!eligible) return "rejected: not an eligible candidate";
                    return NemesisControlApi.TrySelect(requested) ? "ok" : "rejected";
                }
                case "clear":
                    return NemesisControlApi.TryClear() ? "ok" : "rejected";
                case "confirm":
                    return NemesisControlApi.TryConfirmPending() ? "ok" : "rejected";
                case "cancel":
                    return NemesisControlApi.TryCancelPending() ? "ok" : "rejected";
                default:
                    return "unknown action";
            }
        }
    }
}
