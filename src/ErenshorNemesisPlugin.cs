using System;
using System.IO;
using System.Text.RegularExpressions;
using Lunaris;
using Lunaris.Config;
using HarmonyLib;
using UnityEngine;
using ForgottenRoads.StandaloneUi;

namespace ErenshorNemesis
{
    [LunarisPlugin(PluginGuid, PluginVersion, "forgetwhtuno",
        "Automatic persistent rival identity, bounded two-way rivalry chat, and optional PvP/Deep Sims bridges. Social rivalry only.")]
    [LunarisPermission(LunarisPermission.FileAccess | LunarisPermission.Reflection | LunarisPermission.Harmony)]
    public sealed class ErenshorNemesisPlugin : LunarisPlugin
    {
        internal const string PluginGuid = "forgetwhtuno.erenshor.nemesis";
        internal const string PluginVersion = "0.3.0";

        internal static ErenshorNemesisPlugin Instance;
        private Harmony _harmony;
        private NemesisSettings _settings;
        private string _pendingControlSelection;
        private bool _pendingControlSelectionAutomatic;
        private static NemesisControlState _fallbackCachedState;
        private static float _nextFallbackStateRefresh;
        private int _pendingControlAction;
        private NemesisSuiteAuraProvider _auraProvider;

        // Erenshor's current social log still accepts a color argument, but named compatibility
        // colors can materialize as literal rich-text on some builds. Learn the actual native tell
        // color argument from vanilla social-log traffic and otherwise use the one-argument native
        // path. The visible message string never contains a color tag.
        private static string _nativeIncomingTellColor = string.Empty;
        private static string _nativeOutgoingTellColor = string.Empty;
        private static bool _emittingNemesisChat;

        private void Awake()
        {
            Instance = this;
            _settings = new NemesisSettings();
            Config.Register(ref _settings);
            string dataDirectory = Path.Combine(Path.Combine(AppContext.BaseDirectory, "plugins", "config"), "ErenshorNemesis");
            NemesisStateStore state = new NemesisStateStore(Path.Combine(dataDirectory, "nemesis-state.dat"));
            NemesisDirector.SaveSettings = delegate { try { Config.Save(); } catch { } };
            NemesisDirector.Initialize(_settings, state, new LunarisNemesisLog(Logging));
            _harmony = new Harmony(PluginGuid); _harmony.PatchAll();
            try { _auraProvider = new NemesisSuiteAuraProvider(this); }
            catch (Exception ex) { Logging.LogError("Nemesis Aura provider init failed: " + ex); }
            Logging.LogInfo("Erenshor Nemesis " + PluginVersion + " loaded. Rival assignment is automatic; /enemesis status and /enemesis candidates remain available for diagnostics/override.");
            StandaloneFallbackUi.Initialize(this, "nemesis", "NEMESIS",
                "Rival assignment is automatic. Native Friends remain manual-only candidates; explicit selection overrides and persists.", 120f,
                BuildFallbackStatus,
                new FallbackAction("Confirm", NemesisControlApi.TryConfirmPending, delegate { return FallbackState().HasPendingConfirmation; }),
                new FallbackAction("Cancel", NemesisControlApi.TryCancelPending, delegate { return FallbackState().HasPendingConfirmation; }),
                new FallbackAction("Stop Rivalry", NemesisControlApi.TryClear, delegate { return FallbackState().HasNemesis; }));
        }

        private static NemesisControlState FallbackState()
        {
            float now = Time.unscaledTime;
            if (_fallbackCachedState == null || now >= _nextFallbackStateRefresh)
            {
                _fallbackCachedState = NemesisControlApi.GetBasicState();
                _nextFallbackStateRefresh = now + 0.35f;
            }
            return _fallbackCachedState;
        }

        private static string BuildFallbackStatus()
        {
            NemesisControlState state = FallbackState();
            string presentation = state.CandidatePresentation ?? "[Nemesis] Candidates unavailable.";
            if (presentation.StartsWith("[Nemesis] ", StringComparison.Ordinal)) presentation = presentation.Substring(10);
            string status = NemesisHubPresentation.Build(state.Enabled, state.HasNemesis, state.NemesisName,
                state.GrudgePoints, state.Record, state.HasPendingConfirmation,
                state.AutomaticCandidateNames == null ? 0 : state.AutomaticCandidateNames.Length);
            return status + "\n" + presentation;
        }


        private void Update()
        {
            StandaloneFallbackUi.Tick(SuiteUiPolicy.IsGameplayReady());
            try
            {
                int action = _pendingControlAction; _pendingControlAction = 0;
                if (action == 1) NemesisDirector.ControlClear();
                else if (action == 2) NemesisDirector.ControlConfirm();
                else if (action == 3) NemesisDirector.ControlCancelPending();
                if (!string.IsNullOrWhiteSpace(_pendingControlSelection))
                {
                    string selection = _pendingControlSelection;
                    bool automatic = _pendingControlSelectionAutomatic;
                    _pendingControlSelection = null; _pendingControlSelectionAutomatic = false;
                    if (automatic) NemesisDirector.ControlSelectAutomatic(selection); else NemesisDirector.ControlSelect(selection);
                }
                NemesisDirector.Tick();
            }
            catch (Exception ex) { Logging.LogError("Nemesis update failed: " + ex); }
        }

        private void OnDestroy()
        {
            StandaloneFallbackUi.Dispose();
            try { if (_auraProvider != null) _auraProvider.Unregister(); } catch { }
            _auraProvider = null;
            try { if (_harmony != null) _harmony.UnpatchSelf(); } catch { }
            _harmony = null; _pendingControlSelection = null; _pendingControlSelectionAutomatic = false; _pendingControlAction = 0;
            _fallbackCachedState = null; _nextFallbackStateRefresh = 0f;
            _nativeIncomingTellColor = string.Empty; _nativeOutgoingTellColor = string.Empty; _emittingNemesisChat = false;
            NemesisDirector.Shutdown(); SuiteUiPolicy.Reset(); Instance = null;
        }

        internal bool RequestControlSelect(string simName, bool automatic)
        {
            if (string.IsNullOrWhiteSpace(simName)) return false;
            _pendingControlSelection = simName.Trim(); _pendingControlSelectionAutomatic = automatic; _pendingControlAction = 0;
            _fallbackCachedState = null; _nextFallbackStateRefresh = 0f; return true;
        }
        internal bool RequestControlClear() { _pendingControlAction = 1; _pendingControlSelection = null; _pendingControlSelectionAutomatic = false; _fallbackCachedState = null; _nextFallbackStateRefresh = 0f; return true; }
        internal bool RequestControlConfirm() { _pendingControlAction = 2; _pendingControlSelection = null; _pendingControlSelectionAutomatic = false; _fallbackCachedState = null; _nextFallbackStateRefresh = 0f; return true; }
        internal bool RequestControlCancelPending() { _pendingControlAction = 3; _pendingControlSelection = null; _pendingControlSelectionAutomatic = false; _fallbackCachedState = null; _nextFallbackStateRefresh = 0f; return true; }

        internal bool Handle(TypeText input, string raw)
        {
            string text = (raw ?? string.Empty).Trim(); string prefix = null;
            foreach (string candidate in new[] { "/enemesis", "/dsnemesis" })
                if (text.Equals(candidate, StringComparison.OrdinalIgnoreCase) || text.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase)) { prefix = candidate; break; }
            if (prefix != null)
            {
                ClearInput(input);
                NemesisDirector.HandleCommand(text.Length == prefix.Length ? string.Empty : text.Substring(prefix.Length).Trim());
                return true;
            }

            // Strong ownership only: exact CURRENT Nemesis name + directed punctuation. Ordinary
            // party/local chat falls through untouched. This runs before Deep Sims' command patch so
            // a line owned here cannot also trigger a generic party-Sim response.
            string addressedMessage;
            if (NemesisDirector.TryHandleNaturalAddress(text, out addressedMessage))
            {
                ClearInput(input);
                return true;
            }
            return false;
        }

        private static void ClearInput(TypeText input)
        { try { if (input != null && input.typed != null) input.typed.text = string.Empty; } catch { } }

        internal static void ChatSystem(string value)
        {
            _emittingNemesisChat = true;
            try { UpdateSocialLog.LogAdd(value); } catch { }
            finally { _emittingNemesisChat = false; }
        }

        internal static void ChatRivalTell(string value)
        { WriteNativeTell(value, _nativeIncomingTellColor); }

        internal static void ChatOutgoingTell(string value)
        { WriteNativeTell(value, _nativeOutgoingTellColor); }

        private static void WriteNativeTell(string value, string nativeColor)
        {
            _emittingNemesisChat = true;
            try
            {
                if (IsUsableCapturedColor(nativeColor)) UpdateSocialLog.LogAdd(value, nativeColor);
                else UpdateSocialLog.LogAdd(value);
            }
            catch { try { UpdateSocialLog.LogAdd(value); } catch { } }
            finally { _emittingNemesisChat = false; }
        }

        internal static void NoteNativeSocialStyle(string text, string color)
        {
            if (_emittingNemesisChat || string.IsNullOrWhiteSpace(text) || !IsUsableCapturedColor(color)) return;
            string clean;
            try { clean = Regex.Replace(text, @"<[^>]+>", string.Empty).Trim(); }
            catch { clean = text.Trim(); }
            if (clean.Length == 0) return;

            if (Regex.IsMatch(clean, @"^.+?\s+tells you:", RegexOptions.IgnoreCase))
            {
                _nativeIncomingTellColor = color;
                return;
            }
            if (clean.StartsWith("You tell ", StringComparison.OrdinalIgnoreCase) &&
                !clean.StartsWith("You tell the group:", StringComparison.OrdinalIgnoreCase))
                _nativeOutgoingTellColor = color;
        }

        // These named strings are legacy compatibility values, not evidence of a native style on
        // the running build. Never learn them from another mod and risk recreating literal markup.
        private static bool IsUsableCapturedColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return false;
            string value = color.Trim();
            return !value.Equals("magenta", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("cyan", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("lightblue", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("yellow", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("red", StringComparison.OrdinalIgnoreCase);
        }

        internal static string ChatStyleStatus()
        {
            return IsUsableCapturedColor(_nativeIncomingTellColor) ? "native-tell-captured" : "native-default-no-markup";
        }
    }

    [HarmonyPatch(typeof(TypeText), "CheckCommands")]
    [HarmonyBefore("forgetwhtuno.erenshor.deepsims")]
    internal static class NemesisChatPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(TypeText __instance)
        { try { return ErenshorNemesisPlugin.Instance == null || !ErenshorNemesisPlugin.Instance.Handle(__instance, __instance == null || __instance.typed == null ? "" : __instance.typed.text); } catch { return true; } }
    }

    [HarmonyPatch(typeof(UpdateSocialLog), "LogAdd", new Type[] { typeof(string), typeof(string) })]
    internal static class NemesisNativeChatStylePatch
    {
        [HarmonyPostfix]
        private static void Postfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 2) return;
                ErenshorNemesisPlugin.NoteNativeSocialStyle(__args[0] as string, __args[1] as string);
            }
            catch { }
        }
    }
}
