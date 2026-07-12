using System.Collections.Generic;
using System.Linq;
using MfmeFmlDecoder.src.Decoder.Component.Helper;
using MfmeFmlDecoder.src.Model.Component;
using Xunit;

namespace MfmeFmlDecoder.Tests.Decoder.Component.Helper
{
    public class BorderOwnershipAssignerTests
    {
        [Fact]
        public void Scenario1_OffsetBorders_SplitsByLocalOffsets()
        {
            // Background 0, borders at ordinals after lamps in file order from the report table:
            // Border (234,141), Border (224,113), then four lamps — use geometry from scenario 1.
            var borderA = Border(234, 141, 39, 75, ordinal: 1);
            var borderB = Border(224, 113, 39, 75, ordinal: 2);
            var lampB0 = Lamp(225, 114, 17, 17, ordinal: 3);
            var lampB1 = Lamp(245, 170, 17, 17, ordinal: 4);
            var lampA0 = Lamp(235, 142, 17, 17, ordinal: 5);
            var lampA1 = Lamp(255, 198, 17, 17, ordinal: 6);

            var result = BorderOwnershipAssigner.Assign(
                new[] { borderA, borderB },
                new BaseComponent[] { lampB0, lampB1, lampA0, lampA1 });

            Assert.Equal(new[] { 5, 6 }, Ordinals(result, borderA));
            Assert.Equal(new[] { 3, 4 }, Ordinals(result, borderB));
            Assert.Empty(result.Orphans);
            Assert.Empty(result.Unresolved);
        }

        [Fact]
        public void Scenario2_SharedPixel_UsesRelativeOffsets()
        {
            var lamp0 = Lamp(225, 114, 17, 17, ordinal: 1);
            var lamp1a = Lamp(245, 170, 17, 17, ordinal: 2);
            var lamp0b = Lamp(245, 170, 17, 17, ordinal: 3);
            var lamp1b = Lamp(265, 226, 17, 17, ordinal: 4);
            var borderA = Border(224, 113, 39, 75, ordinal: 5);
            var borderB = Border(244, 169, 39, 75, ordinal: 6);

            var result = BorderOwnershipAssigner.Assign(
                new[] { borderA, borderB },
                new BaseComponent[] { lamp0, lamp1a, lamp0b, lamp1b });

            Assert.Equal(new[] { 1, 2 }, Ordinals(result, borderA));
            Assert.Equal(new[] { 3, 4 }, Ordinals(result, borderB));
            Assert.Empty(result.Orphans);
            Assert.Empty(result.Unresolved);
        }

        [Fact]
        public void Scenario4_StackedWithOrdinals_PairsByRisingOrdinal()
        {
            var lamp1 = Lamp(225, 114, 17, 17, ordinal: 1);
            var lamp2 = Lamp(245, 170, 17, 17, ordinal: 2);
            var lamp3 = Lamp(225, 114, 17, 17, ordinal: 3);
            var lamp4 = Lamp(245, 170, 17, 17, ordinal: 4);
            var border5 = Border(224, 113, 39, 75, ordinal: 5);
            var border6 = Border(224, 113, 39, 75, ordinal: 6);

            var result = BorderOwnershipAssigner.Assign(
                new[] { border5, border6 },
                new BaseComponent[] { lamp1, lamp2, lamp3, lamp4 });

            Assert.Equal(new[] { 1, 2 }, Ordinals(result, border5));
            Assert.Equal(new[] { 3, 4 }, Ordinals(result, border6));
            Assert.Empty(result.Orphans);
            Assert.Empty(result.Unresolved);
        }

        [Fact]
        public void Scenario5_LampAboveBorderAndOrphan_RelaxedOrdinalRule()
        {
            var lamp1 = Lamp(225, 114, 17, 17, ordinal: 1);
            var orphan = Lamp(380, 182, 17, 17, ordinal: 2);
            var lamp3 = Lamp(245, 170, 17, 17, ordinal: 3);
            var lamp4 = Lamp(245, 170, 17, 17, ordinal: 4);
            var border5 = Border(224, 113, 39, 75, ordinal: 5);
            var border6 = Border(224, 113, 39, 75, ordinal: 6);
            var lamp7 = Lamp(225, 114, 17, 17, ordinal: 7);

            var result = BorderOwnershipAssigner.Assign(
                new[] { border5, border6 },
                new BaseComponent[] { lamp1, orphan, lamp3, lamp4, lamp7 });

            Assert.Equal(new[] { 1, 3 }, Ordinals(result, border5));
            Assert.Equal(new[] { 4, 7 }, Ordinals(result, border6));
            Assert.Equal(new[] { 2 }, result.Orphans.Select(c => c.OrdinalComponentIdentifier!.Value));
            Assert.Empty(result.Unresolved);
        }

        [Fact]
        public void Annotate_SetsOwnedOrdinalArraysOnBorders()
        {
            var background = new Background
            {
                X = 0,
                Y = 0,
                Width = 1008,
                Height = 676,
            };
            var border5 = Border(224, 113, 39, 75, ordinal: 0);
            var border6 = Border(224, 113, 39, 75, ordinal: 0);
            var lamp1 = Lamp(225, 114, 17, 17, ordinal: 0);
            var lamp2 = Lamp(245, 170, 17, 17, ordinal: 0);
            var lamp3 = Lamp(225, 114, 17, 17, ordinal: 0);
            var lamp4 = Lamp(245, 170, 17, 17, ordinal: 0);

            // File order matching scenario 4 (background, lamps, borders).
            var components = new List<BaseComponent>
            {
                background,
                lamp1,
                lamp2,
                lamp3,
                lamp4,
                border5,
                border6,
            };

            BorderOwnershipAssigner.Annotate(components);

            Assert.Equal(5, border5.OrdinalComponentIdentifier);
            Assert.Equal(6, border6.OrdinalComponentIdentifier);
            Assert.Equal(new[] { 1, 2 }, border5.OwnedOrdinalComponentIdentifiers);
            Assert.Equal(new[] { 3, 4 }, border6.OwnedOrdinalComponentIdentifiers);

            string json = border5.ToJsonObject()["OwnedOrdinalComponentIdentifiers"]!.ToJsonString();
            Assert.Equal("[1,2]", json);
        }

        private static int[] Ordinals(BorderOwnershipAssigner.Result result, Border border) =>
            result.Assignment[border]
                .Select(c => c.OrdinalComponentIdentifier!.Value)
                .OrderBy(id => id)
                .ToArray();

        private static Border Border(uint x, uint y, uint w, uint h, int ordinal) =>
            new Border
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                OrdinalComponentIdentifier = ordinal,
            };

        private static Lamp Lamp(uint x, uint y, uint w, uint h, int ordinal) =>
            new Lamp
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                OrdinalComponentIdentifier = ordinal,
            };
    }
}
