using System;
using System.IO;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;

namespace ErenshorNemesis
{
    [LunarisPlugin("forgetwhtuno.erenshor.nemesis", "0.2.0", "forgetwhtuno",
        "Bounded persistent rival identity, grudge/dialogue/cadence, and optional PvP/Deep Sims bridges. Social rivalry only.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorNemesisPlugin : LunarisPlugin
    {
        internal static ErenshorNemesisPlugin Instance;
        private Harmony _harmony;
        private NemesisSettings _settings;

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
            Logging.LogInfo("Erenshor Nemesis 0.2.0 loaded. Use /enemesis candidates, /enemesis select <Sim>, and /enemesis status.");
        }
        private void Update() { try { NemesisDirector.Tick(); } catch (Exception ex) { Logging.LogError("Nemesis update failed: " + ex); } }
        private void OnDestroy() { try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { } _harmony = null; NemesisDirector.Shutdown(); Instance = null; }

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
