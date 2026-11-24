using System.Collections.Generic;
using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests
{
    // 스테이지 1→5 연속·쉼터·저장 슬롯 규칙 (Chunks/RunFlowRule·RestRule·SaveSlotRule).
    public class RunFlowRuleTests
    {
        [Test]
        public void StageFiveIsLastAndRestOnlyBetweenStages()
        {
            for (int s = 1; s < 5; s++)
            {
                Assert.IsFalse(RunFlowRule.IsFinalStage(s));
                Assert.IsTrue(RunFlowRule.HasRest(s, false));
                Assert.IsFalse(RunFlowRule.HasRest(s, true), "기능 테스트는 쉼터 없음");
                Assert.AreEqual(s + 1, RunFlowRule.NextStage(s));
            }
            Assert.IsTrue(RunFlowRule.IsFinalStage(5));
            Assert.IsFalse(RunFlowRule.HasRest(5, false));
            Assert.AreEqual(5, RunFlowRule.NextStage(5));
        }

        [Test]
        public void StageSeedKeepsStageOneAndDiffersLater()
        {
            for (int seed = 1; seed < 200; seed++)
            {
                Assert.AreEqual(seed, RunFlowRule.StageSeed(seed, 1), "스테이지 1은 판 시드 그대로 (예전과 같은 맵)");
                var seen = new HashSet<int> { seed };
                for (int s = 2; s <= 5; s++)
                {
                    int stageSeed = RunFlowRule.StageSeed(seed, s);
                    Assert.AreEqual(stageSeed, RunFlowRule.StageSeed(seed, s), "같은 판이면 같은 시드");
                    Assert.That(stageSeed, Is.InRange(1, 999999));
                    seen.Add(stageSeed);
                }
                Assert.GreaterOrEqual(seen.Count, 4, $"시드 {seed}: 스테이지마다 맵이 거의 다 달라야 한다");
            }
        }

        [Test]
        public void SaveSlotsWrapAndSlotOneKeepsOldKey()
        {
            Assert.AreEqual("Run.Save", SaveSlotRule.Key("Run.Save", 1));
            Assert.AreEqual("Slot07.Run.Save", SaveSlotRule.Key("Run.Save", 7));
            Assert.AreEqual("Slot99.Hidden.Revivals", SaveSlotRule.Key("Hidden.Revivals", 99));
            Assert.AreEqual(1, SaveSlotRule.Step(99, 1));
            Assert.AreEqual(99, SaveSlotRule.Step(1, -1));
            Assert.AreEqual(43, SaveSlotRule.Step(42, 1));
            Assert.AreEqual(1, SaveSlotRule.Clamp(0));
            Assert.AreEqual(99, SaveSlotRule.Clamp(500));
            Assert.AreEqual("슬롯 05", SaveSlotRule.Label(5));
        }

        [Test]
        public void RestSpringFillsAndLineIsStable()
        {
            Assert.AreEqual(12f, RestRule.SpringHeal(3f, 12f));
            Assert.AreEqual(3f, RestRule.SpringHeal(3f, 0f));
            Assert.AreEqual(RestRule.Line(77, 2), RestRule.Line(77, 2));
            Assert.IsNotEmpty(RestRule.Line(-5, 4));
            Assert.AreNotEqual(RestRule.ShopRoom(2), RestRule.ShopRoom(3));
        }
    }
}
