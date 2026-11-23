using IBlameYou.Player;
using NUnit.Framework;
using UnityEngine;

namespace IBlameYou.Tests
{
    // KeyBindingRules는 UnityEngine을 모르고 KeyCode를 정수로 다룬다. 그 정수가 실제 KeyCode 값과 같은지 (CI에서만 돈다).
    public class KeyCodeValueTests
    {
        [Test]
        public void RuleConstants_MatchUnityKeyCodes()
        {
            Assert.AreEqual((int)KeyCode.Backspace, KeyBindingRules.Backspace);
            Assert.AreEqual((int)KeyCode.Tab, KeyBindingRules.Tab);
            Assert.AreEqual((int)KeyCode.Return, KeyBindingRules.Return);
            Assert.AreEqual((int)KeyCode.Escape, KeyBindingRules.Escape);
            Assert.AreEqual((int)KeyCode.Space, KeyBindingRules.Space);
            Assert.AreEqual((int)KeyCode.Alpha0, KeyBindingRules.Alpha0);
            Assert.AreEqual((int)KeyCode.Alpha9, KeyBindingRules.Alpha9);
            Assert.AreEqual((int)KeyCode.BackQuote, KeyBindingRules.BackQuote);
            Assert.AreEqual((int)KeyCode.A, KeyBindingRules.A);
            Assert.AreEqual((int)KeyCode.C, KeyBindingRules.C);
            Assert.AreEqual((int)KeyCode.D, KeyBindingRules.D);
            Assert.AreEqual((int)KeyCode.M, KeyBindingRules.M);
            Assert.AreEqual((int)KeyCode.R, KeyBindingRules.R);
            Assert.AreEqual((int)KeyCode.S, KeyBindingRules.S);
            Assert.AreEqual((int)KeyCode.W, KeyBindingRules.W);
            Assert.AreEqual((int)KeyCode.Z, KeyBindingRules.Z);
            Assert.AreEqual((int)KeyCode.Keypad0, KeyBindingRules.Keypad0);
            Assert.AreEqual((int)KeyCode.Keypad9, KeyBindingRules.Keypad9);
            Assert.AreEqual((int)KeyCode.KeypadEnter, KeyBindingRules.KeypadEnter);
            Assert.AreEqual((int)KeyCode.KeypadPlus, 270);
            Assert.AreEqual((int)KeyCode.UpArrow, KeyBindingRules.UpArrow);
            Assert.AreEqual((int)KeyCode.DownArrow, KeyBindingRules.DownArrow);
            Assert.AreEqual((int)KeyCode.RightArrow, KeyBindingRules.RightArrow);
            Assert.AreEqual((int)KeyCode.LeftArrow, KeyBindingRules.LeftArrow);
            Assert.AreEqual((int)KeyCode.F1, KeyBindingRules.F1);
            Assert.AreEqual((int)KeyCode.F15, KeyBindingRules.F15);
            Assert.AreEqual((int)KeyCode.RightShift, KeyBindingRules.RightShift);
            Assert.AreEqual((int)KeyCode.LeftShift, KeyBindingRules.LeftShift);
            Assert.AreEqual((int)KeyCode.RightControl, KeyBindingRules.RightControl);
            Assert.AreEqual((int)KeyCode.LeftControl, KeyBindingRules.LeftControl);
            Assert.AreEqual((int)KeyCode.RightAlt, KeyBindingRules.RightAlt);
            Assert.AreEqual((int)KeyCode.LeftAlt, KeyBindingRules.LeftAlt);
            Assert.AreEqual((int)KeyCode.Mouse0, KeyBindingRules.FirstMouseOrJoystick);
            Assert.AreEqual((int)KeyCode.Delete, 127);
            Assert.AreEqual((int)KeyCode.CapsLock, 301);
            Assert.AreEqual((int)KeyCode.PageDown, 281);
        }
    }
}
