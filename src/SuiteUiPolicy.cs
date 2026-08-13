using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorNemesis
{
    // Standalone/Hub UI readiness policy. Canonical v1 (integration handoff). No compile-time
    // dependency on Suite Hub. See CONTRACT_RECONCILIATION.md "Readiness contract".
    //
    // Nemesis has no dedicated panel and no standalone launcher (SUITE_UI_MOD_CONTRACT.md panel
    // table), so only the readiness gate is needed here - action requests coming in through the
    // Aura provider (or any future Hub control surface) must not be honored outside a stable
    // gameplay state, even though NemesisDirector's own lighter-weight Ready() check continues to
    // gate the standalone /enemesis command surface unchanged.
    internal static class SuiteUiPolicy
    {
        private const float StableReadySeconds = 1.0f;

        private static float _rawReadySince = -1f;
        private static int _readySceneHandle = int.MinValue;
        private static bool _canMoveLatched;
        private static bool _acquired;

        internal static bool IsGameplayReady()
        {
            if (!RawGameplayReady())
            {
                _rawReadySince = -1f;
                _readySceneHandle = int.MinValue;
                _canMoveLatched = false;
                _acquired = false;
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (_readySceneHandle != scene.handle)
            {
                _readySceneHandle = scene.handle;
                _rawReadySince = Time.unscaledTime;
                _canMoveLatched = false;
                _acquired = false;
            }
            if (_rawReadySince < 0f) _rawReadySince = Time.unscaledTime;

            if (_acquired)
            {
                // Once Ready is acquired, native UI temporarily setting CanMove=false must not
                // revoke it - do not re-check CanMove here.
                return true;
            }

            try { if (GameData.PlayerControl != null && GameData.PlayerControl.CanMove) _canMoveLatched = true; }
            catch { }

            if (!_canMoveLatched) return false;
            if (Time.unscaledTime - _rawReadySince < StableReadySeconds) return false;

            _acquired = true;
            return true;
        }

        internal static void Reset()
        {
            _rawReadySince = -1f;
            _readySceneHandle = int.MinValue;
            _canMoveLatched = false;
            _acquired = false;
        }

        private static bool RawGameplayReady()
        {
            try
            {
                if (GameData.InCharSelect || GameData.Zoning) return false;
                if (GameData.PlayerControl == null || GameData.PlayerControl.Myself == null) return false;
                Character player = GameData.PlayerControl.Myself;
                if (player.MyStats == null || player.gameObject == null || !player.gameObject.activeInHierarchy) return false;

                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded) return false;
                // The local Character is persistent (DontDestroyOnLoad) - do not compare its
                // GameObject's scene to the active zone scene.

                if (GameData.SimMngr == null || GameData.SimPlayerGrouping == null || GameData.GroupMembers == null)
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
