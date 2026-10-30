using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using Priority = Sleeptalker.Scaffold.Priority;

using Sleeptalker.Scaffold;

namespace Sleeptalker.Sleeper2
{
    /// <summary>The assist layer (owner request 2026-10-30): the dice are the
    /// part of CS2 that frustrates, the story is the part worth playing for.
    ///
    /// Two halves, both riding the game's own machinery:
    ///  - Minimum die roll: every die FSM rolls through a "Roll" / "Safe Roll"
    ///    state whose single action is PlayMaker's RandomEvent over six
    ///    transitions named "1".."6" (captured live 2026-10-30). A postfix on
    ///    RandomEvent.GetRandomEvent swaps a face below the minimum for a
    ///    uniform pick among the faces at or above it. The game then runs its
    ///    own face state, stress check and render, so nothing downstream is faked.
    ///  - The developers' DEBUG MENU effects (Letterbox Canvas/DEBUG MENU, an
    ///    inactive canvas in release builds; its FSM decoded live 2026-10-30):
    ///    each is a plain Dialogue System variable write, reproduced here
    ///    verbatim. Dice values reload through the die FSM's own global
    ///    "Check Variables" event (Load From Save path: no stress check).</summary>
    internal static class DiceAssist
    {
        public const int Off = 0;

        public static int Minimum
        {
            get
            {
                int v = Plugin.MinimumDieRoll != null ? Plugin.MinimumDieRoll.Value : 0;
                return v <= 1 ? Off : Mathf.Min(v, 6);
            }
            set
            {
                if (Plugin.MinimumDieRoll != null)
                    Plugin.MinimumDieRoll.Value = value <= 1 ? Off : Mathf.Min(value, 6);
            }
        }

        // ---------- Minimum die roll (Harmony) ----------

        [HarmonyPatch(typeof(RandomEvent), "GetRandomEvent")]
        private static class RandomEventPatch
        {
            private static void Postfix(RandomEvent __instance, ref FsmEvent __result)
            {
                try
                {
                    int min = Minimum;
                    if (min == Off || __result == null) return;
                    var state = __instance.State;
                    var fsm = __instance.Fsm;
                    if (state == null || fsm == null || fsm.GameObject == null) return;
                    if (fsm.GameObject.name != "Die") return;
                    if (state.Name == null || state.Name.IndexOf("Roll") < 0) return;
                    if (!int.TryParse(__result.Name, out int face) || face >= min) return;

                    // Only a pure die-face state qualifies: every transition a face.
                    var allowed = new List<FsmEvent>();
                    foreach (var t in state.Transitions)
                    {
                        if (t.FsmEvent == null || !int.TryParse(t.FsmEvent.Name, out int f)
                            || f < 1 || f > 6)
                            return;
                        if (f >= min) allowed.Add(t.FsmEvent);
                    }
                    if (allowed.Count == 0) return;
                    var pick = allowed[Random.Range(0, allowed.Count)];
                    if (Plugin.TraceFsmSignals.Value)
                        Plugin.Log.LogInfo("[Assist] " + Util.PathOf(fsm.GameObject)
                            + " " + state.Name + ": " + face + " -> " + pick.Name);
                    __result = pick;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning("[Assist] roll patch: " + e.Message);
                }
            }
        }

        // ---------- Player dice ----------

        private static string DiePath(int n)
            => "Letterbox Canvas/Top UI/Dice UI/Dice Slot " + n + "/Die";

        private static PlayMakerFSM DieFsm(int n)
        {
            var go = GameObject.Find(DiePath(n));
            if (go == null || !go.activeInHierarchy) return null;
            return go.GetComponent<PlayMakerFSM>();
        }

        private static string LuaName(PlayMakerFSM fsm, int n)
        {
            var v = fsm.FsmVariables.GetFsmString("Die Name");
            return v != null && !string.IsNullOrEmpty(v.Value) ? v.Value : "Die" + n;
        }

        /// <summary>True while the die picker or a slotted die owns the dice:
        /// reloading a die out from under an allocation would desync the card.</summary>
        public static bool DiceBusy()
        {
            if (ModeModel.Current() == Mode.DiceAllocation) return true;
            for (int n = 1; n <= 5; n++)
            {
                var fsm = DieFsm(n);
                if (fsm == null) continue;
                string s = fsm.ActiveStateName ?? "";
                if (s != "Idle" && s != "Used" && s != "Broken") return true;
            }
            return false;
        }

        public static bool AnyDice()
        {
            for (int n = 1; n <= 5; n++) if (DieFsm(n) != null) return true;
            return false;
        }

        /// <summary>Every usable player die below 6 becomes 6 (the debug menu's
        /// "ALL 6S" button name, done the save-load way).</summary>
        public static string RaiseAll()
        {
            if (!AnyDice()) return Lex.T("assist.no-dice");
            if (DiceBusy()) return Lex.T("assist.dice-busy");
            int raised = 0;
            for (int n = 1; n <= 5; n++)
            {
                var fsm = DieFsm(n);
                if (fsm == null || fsm.ActiveStateName != "Idle") continue;
                string lua = LuaName(fsm, n);
                float v = DialogueLua.GetVariable(lua).asFloat;
                if (v < 1f || v > 5f) continue; // 0 spent, 6 already, 9 glitched
                DialogueLua.SetVariable(lua, 6);
                fsm.SendEvent("Check Variables");
                raised++;
            }
            if (raised == 0) return Lex.T("assist.raise-none");
            QueueDiceRead();
            return Lex.T("assist.raised");
        }

        /// <summary>The debug menu's REROLL DICE: the dice's own Reroll event.
        /// Spent dice stay spent; rolls obey the minimum.</summary>
        public static string RerollAll()
        {
            if (!AnyDice()) return Lex.T("assist.no-dice");
            if (DiceBusy()) return Lex.T("assist.dice-busy");
            int sent = 0;
            for (int n = 1; n <= 5; n++)
            {
                var fsm = DieFsm(n);
                if (fsm == null || fsm.ActiveStateName != "Idle") continue;
                fsm.SendEvent("Reroll");
                sent++;
            }
            if (sent == 0) return Lex.T("assist.raise-none");
            QueueDiceRead();
            return Lex.T("assist.rerolling");
        }

        /// <summary>The debug menu's FIX ALL DICE (Die1..5_Health = 3), plus the
        /// reload its own SendEvent never reached (it targeted the menu itself).</summary>
        public static string RepairAll()
        {
            if (DiceBusy()) return Lex.T("assist.dice-busy");
            int repaired = 0;
            for (int n = 1; n <= 5; n++)
            {
                string key = "Die" + n + "_Health";
                if (DialogueLua.GetVariable(key).asFloat < 3f) repaired++;
                DialogueLua.SetVariable(key, 3);
            }
            for (int n = 1; n <= 5; n++)
            {
                var fsm = DieFsm(n);
                if (fsm != null) fsm.SendEvent("Check Variables");
            }
            if (repaired == 0) return Lex.T("assist.repair-none");
            QueueDiceRead();
            return repaired + " " + Lex.T(repaired == 1 ? "assist.repaired-one" : "assist.repaired");
        }

        // ---------- Vitals (debug menu states, verbatim) ----------

        public static string FillEnergy()
        {
            DialogueLua.SetVariable("Player_Energy", 100);
            return Lex.T("assist.energy-filled");
        }

        public static string LowerStress()
        {
            float s = DialogueLua.GetVariable("Player_Stress").asFloat;
            if (s <= 0f) return Lex.T("assist.stress-zero");
            DialogueLua.SetVariable("Player_Stress", s - 1f);
            return Lex.T("assist.stress-now") + " " + (s - 1f).ToString("0") + ".";
        }

        private static string _verifyVar, _verifyLabel;
        private static float _verifyBefore, _verifyAt;

        /// <summary>Adds to a resource and answers a beat later with what
        /// actually landed: fuel and supplies are capped by the rig's stowage
        /// (live 2026-10-30: Player_Supplies 6 snapped back to 5 within frames),
        /// so "now 6" spoken at write time would be a lie. Null = answer pending.</summary>
        public static string Add(string variable, int amount, string labelKey)
        {
            float before = DialogueLua.GetVariable(variable).asFloat;
            DialogueLua.SetVariable(variable, before + amount);
            _verifyVar = variable;
            _verifyLabel = labelKey;
            _verifyBefore = before;
            _verifyAt = Time.unscaledTime + 0.3f;
            return null;
        }

        private static void VerifyTick()
        {
            if (_verifyVar == null || Time.unscaledTime < _verifyAt) return;
            float now = DialogueLua.GetVariable(_verifyVar).asFloat;
            string line = now > _verifyBefore
                ? Lex.T(_verifyLabel) + " " + now.ToString("0") + "."
                : Lex.T("assist.at-max").Replace("{n}", now.ToString("0"));
            Plugin.Log.LogInfo("[Assist] " + _verifyVar + " " + _verifyBefore + " -> " + now);
            _verifyVar = null;
            SpeechService.Say(line, Priority.Immediate, "assist");
        }

        public static string Amount(string variable)
            => DialogueLua.GetVariable(variable).asFloat.ToString("0");

        // ---------- Settled dice read ----------

        private static bool _readPending;
        private static float _readAt;

        private static void QueueDiceRead()
        {
            _readPending = true;
            _readAt = Time.unscaledTime + 0.4f;
        }

        public static void Tick()
        {
            VerifyTick();
            if (!_readPending || Time.unscaledTime < _readAt) return;
            if (!DiceFlow.AllDiceSettled())
            {
                if (Time.unscaledTime - _readAt > 5f) _readPending = false; // never hang
                return;
            }
            _readPending = false;
            var sb = new System.Text.StringBuilder();
            for (int n = 1; n <= 5; n++)
            {
                string line = DiceFlow.DieLine(n);
                if (line == null) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(line);
            }
            if (sb.Length > 0) SpeechService.Say(sb.ToString(), Priority.Queued, "assist");
        }
    }
}
