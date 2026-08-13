using System;
using System.IO;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;

namespace ErenshorNemesis
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Bounded persistent rival identity, grudge/dialogue/cadence, and optional PvP/Deep Sims bridges. Social rivalry only.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorNemesisPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.nemesis";
        internal const string PluginVersion = "0.2.0";

        internal static ErenshorNemesisPlugin Instance;
        private Harmony _harmony;
        private NemesisSettings _settings;
        private string _pendingControlSelection;
        private int _pendingControlAction;
        private NemesisSuiteAuraProvider _auraProvider;

        private void Awake()
        {
            Instance = this;
            _settings = new NemesisSettings();
            Config.Register(ref _settings);
            string dataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorNemesis");
            NemesisStateStore state = new NemesisStateStore(Path.Combine(dataDirectory, "nemesis-state.dat"));
            NemesisDirector.SaveSettings = delegate { try { Config.Save(); } catch { } };
            NemesisDirector.Initialize(_settings, state, new LunarisNemesisLog(Logging));
            _harmony = new Harmony("forgetwhtuno.erenshor.nemesis"); _harmony.PatchAll();
            try { _auraProvider = new NemesisSuiteAuraProvider(this); }
            catch (Exception ex) { Logging.LogError("Nemesis Aura provider init failed: " + ex); }
            Logging.LogInfo("Erenshor Nemesis 0.2.0 loaded. Use /enemesis candidates, /enemesis select <Sim>, and /enemesis status.");
        }
        private void Update()
        {
            try
            {
                int action = _pendingControlAction; _pendingControlAction = 0;
                if (action == 1) NemesisDirector.ControlClear();
                else if (action == 2) NemesisDirector.ControlConfirm();
                else if (action == 3) NemesisDirector.ControlCancelPending();
                if (!string.IsNullOrWhiteSpace(_pendingControlSelection))
                {
                    string selection = _pendingControlSelection; _pendingControlSelection = null; NemesisDirector.ControlSelect(selection);
                }
                NemesisDirector.Tick();
            }
            catch (Exception ex) { Logging.LogError("Nemesis update failed: " + ex); }
        }
        private void OnDestroy()
        {
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null; _pendingControlSelection = null; _pendingControlAction = 0;
            NemesisDirector.Shutdown(); SuiteUiPolicy.Reset(); Instance = null;
        }

        internal bool RequestControlSelect(string simName) { if (string.IsNullOrWhiteSpace(simName)) return false; _pendingControlSelection = simName.Trim(); _pendingControlAction = 0; return true; }
        internal bool RequestControlClear() { _pendingControlAction = 1; _pendingControlSelection = null; return true; }
        internal bool RequestControlConfirm() { _pendingControlAction = 2; _pendingControlSelection = null; return true; }
        internal bool RequestControlCancelPending() { _pendingControlAction = 3; _pendingControlSelection = null; return true; }

        internal bool Handle(TypeText input, string raw)
        {
            string text = (raw ?? string.Empty).Trim(); string prefix = null;
            foreach (string candidate in new[] { "/enemesis", "/dsnemesis" })
                if (text.Equals(candidate, StringComparison.OrdinalIgnoreCase) || text.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase)) { prefix = candidate; break; }
            if (prefix == null) return false;
            try { if (input != null && input.typed != null) input.typed.text = string.Empty; } catch { }
            NemesisDirector.HandleCommand(text.Length == prefix.Length ? string.Empty : text.Substring(prefix.Length).Trim()); return true;
        }

        internal static void Chat(string value, string color = "lightblue")
        { try { UpdateSocialLog.LogAdd(value, color); } catch { try { UpdateSocialLog.LogAdd(value); } catch { } } }
    }

    [HarmonyPatch(typeof(TypeText), "CheckCommands")]
    internal static class NemesisChatPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(TypeText __instance)
        { try { return ErenshorNemesisPlugin.Instance == null || !ErenshorNemesisPlugin.Instance.Handle(__instance, __instance == null || __instance.typed == null ? "" : __instance.typed.text); } catch { return true; } }
    }
}
