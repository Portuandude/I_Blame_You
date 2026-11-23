using IBlameYou.Player;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // P1 조작 키 바꾸기 규칙 (KeyBindingRules)
    public class KeyBindingRulesTests
    {
        private static int Key(int[,] b, P1Action a, int slot = 0) => b[(int)a, slot];

        [Test]
        public void Defaults_MatchCurrentControls()
        {
            var b = KeyBindingRules.Defaults();
            Assert.AreEqual(KeyBindingRules.A, Key(b, P1Action.Left));
            Assert.AreEqual(KeyBindingRules.D, Key(b, P1Action.Right));
            Assert.AreEqual(KeyBindingRules.W, Key(b, P1Action.Up));
            Assert.AreEqual(KeyBindingRules.S, Key(b, P1Action.Down));
            Assert.AreEqual(KeyBindingRules.Space, Key(b, P1Action.Jump));
            Assert.AreEqual(KeyBindingRules.LeftShift, Key(b, P1Action.Run));
            Assert.AreEqual(KeyBindingRules.RightShift, Key(b, P1Action.Run, 1));
            Assert.AreEqual(KeyBindingRules.LeftControl, Key(b, P1Action.Dash));
            Assert.AreEqual(KeyBindingRules.Return, Key(b, P1Action.Teleport));
            Assert.AreEqual(KeyBindingRules.KeypadEnter, Key(b, P1Action.Teleport, 1));
            Assert.IsFalse(KeyBindingRules.HasDuplicates(b));
            for (int i = 0; i < KeyBindingRules.ActionCount; i++)
                for (int s = 0; s < KeyBindingRules.Slots; s++)
                    if (b[i, s] != KeyBindingRules.None) Assert.IsFalse(KeyBindingRules.IsForbidden(b[i, s]), $"기본 키 {b[i, s]}가 금지 키");
        }

        [Test]
        public void Forbidden_ReservedKeys()
        {
            foreach (int code in new[] { 0, KeyBindingRules.Escape, KeyBindingRules.BackQuote, KeyBindingRules.Tab, KeyBindingRules.C,
                         KeyBindingRules.R, KeyBindingRules.M, KeyBindingRules.Alpha0, 49, KeyBindingRules.Alpha9, KeyBindingRules.F1, 293 /*F12*/,
                         KeyBindingRules.F15, KeyBindingRules.FirstMouseOrJoystick, 330, -5, 3 })
                Assert.IsTrue(KeyBindingRules.IsForbidden(code), $"{code}");
            foreach (int code in new[] { KeyBindingRules.Z, KeyBindingRules.Space, KeyBindingRules.UpArrow, KeyBindingRules.LeftAlt, KeyBindingRules.Keypad0, 106 /*J*/ })
                Assert.IsFalse(KeyBindingRules.IsForbidden(code), $"{code}");
        }

        [Test]
        public void Assign_FreeKey_SetsAndClearsSecondSlot()
        {
            var b = KeyBindingRules.Defaults();
            var r = KeyBindingRules.Assign(b, P1Action.Run, KeyBindingRules.Z, out _);
            Assert.AreEqual(KeyBindingRules.AssignResult.Set, r);
            Assert.AreEqual(KeyBindingRules.Z, Key(b, P1Action.Run));
            Assert.AreEqual(KeyBindingRules.None, Key(b, P1Action.Run, 1));
            Assert.IsFalse(KeyBindingRules.HasDuplicates(b));
        }

        [Test]
        public void Assign_OwnKey_Unchanged_KeepsBothShifts()
        {
            var b = KeyBindingRules.Defaults();
            Assert.AreEqual(KeyBindingRules.AssignResult.Unchanged, KeyBindingRules.Assign(b, P1Action.Run, KeyBindingRules.RightShift, out _));
            Assert.IsTrue(KeyBindingRules.SameAs(b, KeyBindingRules.Defaults()));
        }

        [Test]
        public void Assign_UsedKey_Swaps()
        {
            var b = KeyBindingRules.Defaults();
            var r = KeyBindingRules.Assign(b, P1Action.Jump, KeyBindingRules.LeftControl, out P1Action other);
            Assert.AreEqual(KeyBindingRules.AssignResult.Swapped, r);
            Assert.AreEqual(P1Action.Dash, other);
            Assert.AreEqual(KeyBindingRules.LeftControl, Key(b, P1Action.Jump));
            Assert.AreEqual(KeyBindingRules.Space, Key(b, P1Action.Dash));
            Assert.IsFalse(KeyBindingRules.HasDuplicates(b));
        }

        [Test]
        public void Assign_SecondSlotOfOther_SwapsIntoThatSlot()
        {
            var b = KeyBindingRules.Defaults();
            var r = KeyBindingRules.Assign(b, P1Action.Jump, KeyBindingRules.KeypadEnter, out P1Action other);
            Assert.AreEqual(KeyBindingRules.AssignResult.Swapped, r);
            Assert.AreEqual(P1Action.Teleport, other);
            Assert.AreEqual(KeyBindingRules.Return, Key(b, P1Action.Teleport));
            Assert.AreEqual(KeyBindingRules.Space, Key(b, P1Action.Teleport, 1));
            Assert.IsFalse(KeyBindingRules.HasDuplicates(b));
        }

        [Test]
        public void Assign_Forbidden_LeavesBindings()
        {
            var b = KeyBindingRules.Defaults();
            Assert.AreEqual(KeyBindingRules.AssignResult.Forbidden, KeyBindingRules.Assign(b, P1Action.Jump, KeyBindingRules.Escape, out _));
            Assert.AreEqual(KeyBindingRules.AssignResult.Forbidden, KeyBindingRules.Assign(b, P1Action.Jump, 50, out _));
            Assert.IsTrue(KeyBindingRules.SameAs(b, KeyBindingRules.Defaults()));
        }

        [Test]
        public void Serialize_RoundTrips()
        {
            var b = KeyBindingRules.Defaults();
            KeyBindingRules.Assign(b, P1Action.Left, KeyBindingRules.LeftArrow, out _);
            KeyBindingRules.Assign(b, P1Action.Right, KeyBindingRules.RightArrow, out _);
            KeyBindingRules.Assign(b, P1Action.Jump, KeyBindingRules.Z, out _);
            string text = KeyBindingRules.Serialize(b);
            StringAssert.StartsWith("v1:", text);
            var back = KeyBindingRules.Parse(text, out bool repaired);
            Assert.IsFalse(repaired);
            Assert.IsTrue(KeyBindingRules.SameAs(b, back));
        }

        [Test]
        public void Parse_EmptyIsDefaultsNotRepaired()
        {
            var b = KeyBindingRules.Parse(null, out bool repaired);
            Assert.IsFalse(repaired);
            Assert.IsTrue(KeyBindingRules.SameAs(b, KeyBindingRules.Defaults()));
            KeyBindingRules.Parse("", out repaired);
            Assert.IsFalse(repaired);
        }

        [Test]
        public void Parse_Broken_FallsBackToDefaults()
        {
            foreach (string text in new[] { "garbage", "v2:97,0", "v1:97,0;100,0", "v1:x,0;100,0;119,0;115,0;32,0;304,303;306,0;13,271", "v1:97;100,0;119,0;115,0;32,0;304,303;306,0;13,271" })
            {
                var b = KeyBindingRules.Parse(text, out bool repaired);
                Assert.IsTrue(repaired, text);
                Assert.IsTrue(KeyBindingRules.SameAs(b, KeyBindingRules.Defaults()), text);
            }
        }

        [Test]
        public void Parse_OneBadAction_OnlyThatResets()
        {
            // 점프를 Z로 바꾼 저장값에서 대쉬만 Esc(금지)로 깨졌다
            var b = KeyBindingRules.Parse("v1:97,0;100,0;119,0;115,0;122,0;304,303;27,0;13,271", out bool repaired);
            Assert.IsTrue(repaired);
            Assert.AreEqual(KeyBindingRules.Z, Key(b, P1Action.Jump));
            Assert.AreEqual(KeyBindingRules.LeftControl, Key(b, P1Action.Dash));
        }

        [Test]
        public void Parse_EmptyFirstSlot_ShiftsSecond()
        {
            var b = KeyBindingRules.Parse("v1:97,0;100,0;119,0;115,0;32,0;0,303;306,0;13,271", out bool repaired);
            Assert.IsFalse(repaired);
            Assert.AreEqual(KeyBindingRules.RightShift, Key(b, P1Action.Run));
            Assert.AreEqual(KeyBindingRules.None, Key(b, P1Action.Run, 1));
        }

        [Test]
        public void Parse_Duplicate_FallsBackToDefaults()
        {
            var b = KeyBindingRules.Parse("v1:97,0;97,0;119,0;115,0;32,0;304,303;306,0;13,271", out bool repaired);
            Assert.IsTrue(repaired);
            Assert.IsTrue(KeyBindingRules.SameAs(b, KeyBindingRules.Defaults()));
        }

        [Test]
        public void Labels_DefaultMatchHelpText()
        {
            var b = KeyBindingRules.Defaults();
            Assert.AreEqual("A", KeyBindingRules.Label(b, P1Action.Left));
            Assert.AreEqual("W", KeyBindingRules.Label(b, P1Action.Up));
            Assert.AreEqual("Space", KeyBindingRules.Label(b, P1Action.Jump));
            Assert.AreEqual("Shift", KeyBindingRules.Label(b, P1Action.Run));
            Assert.AreEqual("L.Ctrl", KeyBindingRules.Label(b, P1Action.Dash));
            Assert.AreEqual("Enter", KeyBindingRules.Label(b, P1Action.Teleport));
            KeyBindingRules.Assign(b, P1Action.Run, KeyBindingRules.LeftShift, out _); // 자기 키 → 그대로
            Assert.AreEqual("Shift", KeyBindingRules.Label(b, P1Action.Run));
            KeyBindingRules.Assign(b, P1Action.Run, KeyBindingRules.Z, out _);
            Assert.AreEqual("Z", KeyBindingRules.Label(b, P1Action.Run));
        }

        [Test]
        public void DisplayName_NeverHasPlus_UnknownUsesFallback()
        {
            for (int code = KeyBindingRules.MinKey; code < KeyBindingRules.FirstMouseOrJoystick; code++)
            {
                string name = KeyBindingRules.DisplayName(code);
                if (name != null) StringAssert.DoesNotContain("+", name, $"{code}");
            }
            var b = KeyBindingRules.Defaults();
            KeyBindingRules.Assign(b, P1Action.Jump, 300 /*Numlock*/, out _);
            Assert.AreEqual("NUM", KeyBindingRules.Label(b, P1Action.Jump, c => "NUM"));
            Assert.AreEqual("#300", KeyBindingRules.Label(b, P1Action.Jump));
        }

        [Test]
        public void ActionNames_AllKorean()
        {
            for (int i = 0; i < KeyBindingRules.ActionCount; i++)
                Assert.AreNotEqual(((P1Action)i).ToString(), KeyBindingRules.ActionName((P1Action)i));
        }
    }
}
