using IBlameYou.Systems;
using NUnit.Framework;

namespace IBlameYou.Tests.EditMode
{
    public class RelicCodexTests
    {
        [Test]
        public void With_AddsBit_HasReadsIt()
        {
            int mask = RelicCodex.With(0, 3);
            mask = RelicCodex.With(mask, 17);
            Assert.IsTrue(RelicCodex.Has(mask, 3));
            Assert.IsTrue(RelicCodex.Has(mask, 17));
            Assert.IsFalse(RelicCodex.Has(mask, 0));
            Assert.AreEqual(mask, RelicCodex.With(mask, 3), "이미 있으면 그대로");
        }

        [Test]
        public void OutOfRange_Ignored()
        {
            Assert.AreEqual(5, RelicCodex.With(5, -1));
            Assert.AreEqual(5, RelicCodex.With(5, 31));
            Assert.IsFalse(RelicCodex.Has(-1, 31));
            Assert.IsFalse(RelicCodex.Has(-1, -1));
        }

        [Test]
        public void Count_And_Summary()
        {
            int mask = RelicCodex.With(RelicCodex.With(RelicCodex.With(0, 0), 5), 20);
            Assert.AreEqual(3, RelicCodex.Count(mask, 21));
            Assert.AreEqual(2, RelicCodex.Count(mask, 18), "전체 수 밖의 번호는 세지 않는다");
            Assert.AreEqual("유물 도감 2/18", RelicCodex.Summary(mask, 18));
            Assert.AreEqual("유물 도감 0/18", RelicCodex.Summary(0, 18));
        }

        [Test]
        public void Complete_Needs_Every_Relic()
        {
            int all3 = RelicCodex.With(RelicCodex.With(RelicCodex.With(0, 0), 1), 2);
            Assert.IsTrue(RelicCodex.Complete(all3, 3));
            Assert.IsFalse(RelicCodex.Complete(all3, 4), "하나 빠지면 미완성");
            Assert.IsTrue(RelicCodex.Complete(all3 | (1 << 9), 3), "전체 수 밖의 번호는 상관없다");
            Assert.IsFalse(RelicCodex.Complete(0, 0), "유물이 없으면 완성도 없다");
            Assert.IsFalse(RelicCodex.Complete(-1, 32), "31종을 넘으면 담을 수 없다");
            Assert.AreEqual("유물 도감 3/3 ★", RelicCodex.Summary(all3, 3));
            Assert.AreEqual("유물 도감 3/4", RelicCodex.Summary(all3, 4));
        }

        [Test]
        public void Grid_WideScreen_KeepsSixColumnsAndFits()
        {
            int columns = RelicCodex.GridColumns(23, 1280f, 720f);
            Assert.AreEqual(6, columns);
            int rows = RelicCodex.GridRows(23, columns);
            Assert.AreEqual(4, rows);
            Assert.LessOrEqual(rows * RelicCodex.GridCellHeight(rows, 720f) + RelicCodex.GridChrome, 720f);
        }

        [Test]
        public void Grid_ShortScreen_AddsColumnsToFit()
        {
            // 높이 360: 줄에 쓸 수 있는 높이 178 → 50짜리 줄은 3줄 → 23개면 8칸 필요 (너비 1000이면 8칸까지 된다)
            int columns = RelicCodex.GridColumns(23, 1000f, 360f);
            Assert.AreEqual(8, columns, "높이 178에 50짜리 줄은 3줄까지 → 8칸");
            Assert.LessOrEqual(RelicCodex.GridRows(23, columns) * RelicCodex.GridMinCellHeight + RelicCodex.GridChrome, 360f);
        }

        [Test]
        public void Grid_NarrowScreen_AtLeastThreeColumns()
        {
            Assert.AreEqual(3, RelicCodex.GridColumns(23, 300f, 1000f));
            Assert.AreEqual(4, RelicCodex.GridColumns(23, 500f, 2000f), "너비 500이면 4칸까지");
        }

        [Test]
        public void Grid_CellHeight_Clamped()
        {
            Assert.AreEqual(RelicCodex.GridMaxCellHeight, RelicCodex.GridCellHeight(4, 2000f));
            Assert.AreEqual(RelicCodex.GridMinCellHeight, RelicCodex.GridCellHeight(8, 300f));
        }
    }
}
