using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sleeptalker.Sleeper2
{
    /// <summary>One activation path for Enter on the location rows (ride
    /// 2026-10-30, Solheim: Enter on OLD RELAY opened OLD FABRICATOR).
    ///
    /// Each Location Button FSM watches the EventSystem selection, and the
    /// game's UI selector puts that selection on the location nearest the
    /// camera centre. Rewired's input module submits that selected button on
    /// Enter, in its own update, before the mod's InputManager runs. On the
    /// station the selection is the selector's pick, not the player's row: OLD
    /// RELAY sits low on the hub, the camera never centres it, so the selector
    /// held a neighbour, the native submit opened the neighbour, and the mod's
    /// own click on OLD RELAY came a frame late, after every location button
    /// had gone uninteractable. Proven live: Navigator.Click on OLD RELAY alone
    /// opened OLD RELAY (DISENGAGE SYSTEMS); a real Enter opened OLD FABRICATOR.
    ///
    /// While the zone table owns the station, the zone table's Commit is the
    /// press: the native submit of a keyboard Enter is skipped. Gamepad, Space
    /// and every other screen keep the native path untouched.</summary>
    [HarmonyPatch]
    internal static class NativeSubmitGate
    {
        private const string ModuleType = "Rewired.Integration.UnityUI.RewiredStandaloneInputModule";

        private static MethodBase _target;

        private static bool Prepare()
        {
            var type = AccessTools.TypeByName(ModuleType);
            _target = type != null ? AccessTools.Method(type, "SendSubmitEventToSelectedObject") : null;
            if (_target == null)
                Plugin.Log.LogWarning("[Input] native submit gate: " + ModuleType
                    + ".SendSubmitEventToSelectedObject not found — gate off");
            return _target != null;
        }

        private static MethodBase TargetMethod() => _target;

        private static bool Prefix(ref bool __result)
        {
            if (!(Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))) return true;
            if (!ZoneTable.Active()) return true;
            __result = false; // the zone table's Commit presses the row's own button
            return false;
        }
    }
}
