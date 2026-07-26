using NUnit.Framework;
using OasisPlayer.RuntimeBuild;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeReelStateTests
    {
        [Test]
        public void PositionsDefaultToZeroAndSetWraps()
        {
            var state = new RuntimeReelState();
            Assert.AreEqual(0f, state.GetPosition(0));
            Assert.True(state.SetPosition(0, 95f));
            Assert.AreEqual(95f, state.GetPosition(0));
            Assert.True(state.SetPosition(0, 97f));
            Assert.AreEqual(1f, state.GetPosition(0));
            Assert.True(state.SetPosition(0, -1f));
            Assert.AreEqual(95f, state.GetPosition(0));
        }

        [Test]
        public void VersionOnlyChangesForValidMaterialChanges()
        {
            var state = new RuntimeReelState();
            Assert.False(state.SetPosition(-1, 1f));
            Assert.False(state.SetPosition(RuntimeReelState.MaximumReelCount, 1f));
            Assert.AreEqual(0, state.Version);
            Assert.True(state.SetPosition(2, 1f));
            Assert.AreEqual(1, state.Version);
            Assert.False(state.SetPosition(2, 1f));
            Assert.AreEqual(1, state.Version);
            Assert.True(state.ClearAll());
            Assert.AreEqual(2, state.Version);
        }

        [Test]
        public void ReelDiagnosticSpeedUsesSharedConversionConstant()
        {
            Assert.AreEqual(.8f, RuntimeReelDevelopmentControls.PositionsPerSecond(.5f), .0001f);
        }
    }
}
