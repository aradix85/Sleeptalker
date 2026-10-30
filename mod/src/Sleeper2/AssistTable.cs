using UnityEngine;
using Priority = Sleeptalker.Scaffold.Priority;

using Sleeptalker.Scaffold;

namespace Sleeptalker.Sleeper2
{
    /// <summary>F5 — the assist menu (owner request 2026-10-30): the HelpTable
    /// shape with a Commit. Row 0 is the minimum-die-roll setting (Left/Right
    /// or Enter change it; saved in the BepInEx config); the other rows are
    /// one-shot actions that speak their result and keep the menu open.
    /// F5/Backspace close; any foreign key retires it silently and falls
    /// through, like the help sheet.</summary>
    internal static class AssistTable
    {
        public static bool Open { get; private set; }

        private static Mode _mode;

        private enum Row { Minimum, Raise, Reroll, Repair, Energy, Stress, Cryo, Fuel, Supplies }

        private static readonly Row[] Rows =
        {
            Row.Minimum, Row.Raise, Row.Reroll, Row.Repair,
            Row.Energy, Row.Stress, Row.Cryo, Row.Fuel, Row.Supplies,
        };

        private static readonly TableEngine Table = new TableEngine
        {
            Rows = () => Rows.Length,
            Cols = row => 1,
            RowSpeech = (r, c) => RowRead(r),
            CellSpeech = (r, c) => RowRead(r),
            SectionOf = row => 0,
            SectionPrefix = s => "",
            EmptyRow = () => Lex.T("help.empty"),
            EmptyCol = () => Lex.T("help.empty"),
            EmptyDetail = () => Lex.T("help.empty"),
            EmptyCommit = () => Lex.T("help.empty"),
            Source = "assist",
        };

        static AssistTable()
        {
            Table.Detail = (row, col) => Table.Say(RowRead(row));
            Table.Commit = (row, col) => Activate(row);
        }

        /// <summary>Null when the menu can open; otherwise why not, in words the
        /// player can act on (ride 2026-10-30: under the pause overlay it said
        /// "needs a loaded game", which sent the player looking for a problem).</summary>
        private static string Unavailable()
        {
            switch (ModeModel.Current())
            {
                case Mode.Title: return Lex.T("assist.unavailable");
                case Mode.Pause: return Lex.T("assist.unavailable-pause");
                case Mode.CycleTransition: return Lex.T("assist.unavailable-cycle");
                case Mode.Travel: return Lex.T("assist.unavailable-travel");
                default: return null;
            }
        }

        public static void Toggle()
        {
            if (Open) { Close(); return; }
            string why = Unavailable();
            if (why != null)
            {
                SpeechService.Say(why, Priority.Immediate, "assist");
                return;
            }
            _mode = ModeModel.Current();
            Open = true;
            Table.Reset();
            SpeechService.Say(Lex.T("assist.title") + " " + RowRead(0),
                Priority.Immediate, "assist");
        }

        private static void Close()
        {
            Open = false;
            SpeechService.Say(Lex.T("assist.closed"), Priority.Immediate, "assist");
        }

        public static bool HandleKeys()
        {
            if (!Open) return false;
            if (ModeModel.Current() != _mode) { Open = false; return false; }
            if (Input.GetKeyDown(KeyCode.F5) || Input.GetKeyDown(KeyCode.Backspace))
            {
                Close();
                return true;
            }
            // The setting row: Left/Right change the minimum (options idiom).
            if (Rows[Mathf.Clamp(Table.Row, 0, Rows.Length - 1)] == Row.Minimum)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow)) { StepMinimum(1); return true; }
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { StepMinimum(-1); return true; }
            }
            if (Table.HandleKeys()) return true;
            if (Input.anyKeyDown) Open = false;
            return false;
        }

        /// <summary>Off, 2, 3, 4, 5, 6 — Enter wraps, Left/Right clamp.</summary>
        private static void StepMinimum(int delta, bool wrap = false)
        {
            int cur = DiceAssist.Minimum;
            int idx = cur == DiceAssist.Off ? 0 : cur - 1; // off=0, 2..6 -> 1..5
            idx += delta;
            if (wrap) idx = (idx + 6) % 6;
            else idx = Mathf.Clamp(idx, 0, 5);
            DiceAssist.Minimum = idx == 0 ? DiceAssist.Off : idx + 1;
            string line = RowRead(0);
            if (DiceAssist.Minimum != DiceAssist.Off)
                line += " " + Lex.T("assist.min-explain").Replace("{n}", DiceAssist.Minimum.ToString());
            Table.Say(line);
        }

        private static void Activate(int row)
        {
            string result;
            switch (Rows[row])
            {
                case Row.Minimum: StepMinimum(1, wrap: true); return;
                case Row.Raise: result = DiceAssist.RaiseAll(); break;
                case Row.Reroll: result = DiceAssist.RerollAll(); break;
                case Row.Repair: result = DiceAssist.RepairAll(); break;
                case Row.Energy: result = DiceAssist.FillEnergy(); break;
                case Row.Stress: result = DiceAssist.LowerStress(); break;
                case Row.Cryo: result = DiceAssist.Add("Player_Bits", 100, "assist.cryo-now"); break;
                case Row.Fuel: result = DiceAssist.Add("Player_Fuel", 1, "assist.fuel-now"); break;
                case Row.Supplies: result = DiceAssist.Add("Player_Supplies", 1, "assist.supplies-now"); break;
                default: return;
            }
            if (result == null) return; // answered a beat later (DiceAssist.Add)
            Plugin.Log.LogInfo("[Assist] " + Rows[row] + ": " + result);
            Table.Say(result);
        }

        private static string RowRead(int row)
        {
            if (row < 0 || row >= Rows.Length) return Lex.T("help.empty");
            switch (Rows[row])
            {
                case Row.Minimum:
                    return Lex.T("assist.row.minimum") + ", "
                        + (DiceAssist.Minimum == DiceAssist.Off
                            ? Lex.T("assist.off") : DiceAssist.Minimum.ToString()) + ".";
                case Row.Raise: return Lex.T("assist.row.raise");
                case Row.Reroll: return Lex.T("assist.row.reroll");
                case Row.Repair: return Lex.T("assist.row.repair");
                case Row.Energy:
                    return Lex.T("assist.row.energy") + ", " + Lex.T("assist.now")
                        + " " + DiceAssist.Amount("Player_Energy") + ".";
                case Row.Stress:
                    return Lex.T("assist.row.stress") + ", " + Lex.T("assist.now")
                        + " " + DiceAssist.Amount("Player_Stress") + ".";
                case Row.Cryo:
                    return Lex.T("assist.row.cryo") + ", " + Lex.T("assist.now")
                        + " " + DiceAssist.Amount("Player_Bits") + ".";
                case Row.Fuel:
                    return Lex.T("assist.row.fuel") + ", " + Lex.T("assist.now")
                        + " " + DiceAssist.Amount("Player_Fuel") + ".";
                case Row.Supplies:
                    return Lex.T("assist.row.supplies") + ", " + Lex.T("assist.now")
                        + " " + DiceAssist.Amount("Player_Supplies") + ".";
            }
            return Lex.T("help.empty");
        }
    }
}
