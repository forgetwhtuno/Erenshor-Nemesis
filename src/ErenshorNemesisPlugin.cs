using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ErenshorNemesis
{
    [BepInPlugin("forgetwhtuno.erenshor.nemesis", "Erenshor Nemesis", "0.2.0")]
    [BepInProcess("Erenshor.exe")]
    public sealed class ErenshorNemesisPlugin : BaseUnityPlugin
    {
        internal static ErenshorNemesisPlugin Instance;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this; NemesisDirector.Initialize(Config, Logger);
            _harmony = new Harmony("forgetwhtuno.erenshor.nemesis"); _harmony.PatchAll();
            Logger.LogInfo("Erenshor Nemesis 0.2.0 loaded. Use /enemesis candidates, /enemesis select <Sim>, and /enemesis status.");
        }
        private void Update() { try { NemesisDirector.Tick(); } catch (Exception ex) { Logger.LogError("Nemesis update failed: " + ex); } }
        private void OnDestroy() { try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { } NemesisDirector.Shutdown(); Instance = null; }

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
