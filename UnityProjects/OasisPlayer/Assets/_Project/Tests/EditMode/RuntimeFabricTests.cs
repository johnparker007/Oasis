using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using OasisPlayer.Fabric;
using OasisPlayer.RuntimeBuild;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeFabricTests
    {
        [Test]
        public async Task WorkerOwnsTimingInputsAudioResetAndShutdownAndRejectsSecondImpactBoard()
        {
            var native = new FakeRuntime();
            using (var runner = new FabricMachineRunner(_ => native))
            {
                await runner.StartAsync("unused", null);
                using (var duplicate = new FabricMachineRunner(_ => new FakeRuntime()))
                    StringAssert.Contains("one active board", Assert.Throws<InvalidOperationException>(() => duplicate.StartAsync("unused", null)).Message);
                runner.SubmitInput(new FabricInput("oasis.switch.32", 32, FabricInputKind.Digital, true));
                runner.SubmitInput(new FabricInput("coin", 0, FabricInputKind.Coin, true, 0, 5));
                await Until(() => runner.SubmittedInputs == 2 && runner.SnapshotCount > 1);
                Assert.Greater(runner.Slices, 0); Assert.Greater(runner.AudioFrames, 0);
                Assert.AreEqual(1, runner.RejectedInputs);
                runner.RequestReset(); await Until(() => runner.ResetCount == 1);
                Assert.Greater(native.Session.Released, 0);
            }
            Assert.True(native.Disposed); Assert.True(native.Session.Disposed); Assert.True(native.Session.Stopped);
            using (var next = new FabricMachineRunner(_ => new FakeRuntime())) await next.StartAsync("unused", null);
        }

        [Test]
        public async Task WorkerFaultIsObservableAndNativeOwnershipIsReleased()
        {
            var native = new FakeRuntime();
            using (var runner = new FabricMachineRunner(_ => native))
            {
                await runner.StartAsync("unused", null); native.Session.Fail = true;
                await Until(() => runner.Failure != null);
                StringAssert.Contains("native test failure", runner.Failure.Message);
            }
            Assert.True(native.Session.Disposed); Assert.True(native.Disposed);
            using (var next = new FabricMachineRunner(_ => new FakeRuntime())) await next.StartAsync("unused", null);
        }

        [Test]
        public void OutputMappingUsesEditorLampQuantizationAmberReversalAndCanonicalSegmentBits()
        {
            var manifest = new MachineRuntimeManifest { anchors = Array.Empty<MachineRuntimeAnchor>(), inputs = Array.Empty<MachineInputDefinition>() };
            var machine = new RuntimeMachine(new ResolvedRuntimeBuild("", manifest, "", new CabinetRuntimeManifest(), "", Array.Empty<MachineRuntimeFaceReference>(), null), null);
            try
            {
                var snapshot = new FabricMachineSnapshot(1, new[] { new FabricLamp("lamp0", 0, true, .5f), new FabricLamp("lamp1", 1, false, 1), new FabricLamp("lamp511", 511, true, 1) },
                    new[] { new FabricReel("r0", 0, 12) }, new[] { new FabricCharacterDisplay("alpha0", new[] { 1u << 8 }, new byte[] { 1 }, .75f) },
                    new[] { new FabricSegmentDisplay("seven0", new[] { 1UL }) });
                RuntimeFabricBehavior.ApplySnapshot(machine, snapshot, new[] { 48 });
                Assert.That(machine.LampState.GetBrightness(0), Is.EqualTo(128f / 255).Within(.0001f));
                Assert.AreEqual(0, machine.LampState.GetBrightness(1)); Assert.AreEqual(1, machine.LampState.GetBrightness(511)); Assert.AreEqual(72, machine.ReelState.GetPosition(0));
                Assert.AreEqual((1 << 10) | (1 << 16), machine.SegmentDisplayState.GetMasks("alpha:0")[0]);
                Assert.AreEqual(.75f, machine.SegmentDisplayState.GetBrightness("alpha:0")[0]);
                Assert.AreEqual(128, machine.SegmentDisplayState.GetMasks("sevenSegment:0")[0]);
            }
            finally { machine.UnloadAssets(); }
        }

        [Test]
        public void InputMappingPreservesZeroBasedDigitalAndDedicatedCoinProtocol()
        {
            var digital = RuntimeFabricBehavior.MapInput(new MachineInputDefinition { id = "door", buttonNumber = "32" }, true);
            Assert.AreEqual("oasis.switch.32", digital.Identifier); Assert.AreEqual(32, digital.NumericalIndex); Assert.True(digital.Active);
            var coin = RuntimeFabricBehavior.MapInput(new MachineInputDefinition { id = "coin", coinInput = true, coinChannel = 2, coinValue = 5 }, false);
            Assert.AreEqual(FabricInputKind.Coin, coin.Kind); Assert.AreEqual(2, coin.CoinChannel); Assert.AreEqual(5, coin.CoinValue); Assert.False(coin.Active);
            Assert.Throws<System.IO.InvalidDataException>(() => RuntimeFabricBehavior.MapInput(new MachineInputDefinition { id = "coin", coinInput = true }, true));
            Assert.Throws<System.IO.InvalidDataException>(() => RuntimeFabricBehavior.MapInput(new MachineInputDefinition { id = "start", buttonNumber = "-1" }, true));
        }

        [Test]
        public void ImpactAlphaCellOrderMatchesTheEditorsImportedReversalContract()
        {
            var alpha = new FaceRuntimeAlphaSegmentDisplayManifestEntry { digitCount = 16, isReversed = false };
            Assert.AreEqual(15, RuntimeSegmentDisplayRenderer.SourceIndex(alpha, 0, "Impact"));
            alpha.isReversed = true;
            Assert.AreEqual(0, RuntimeSegmentDisplayRenderer.SourceIndex(alpha, 0, "Impact"));
            Assert.AreEqual(15, RuntimeSegmentDisplayRenderer.SourceIndex(alpha, 0, null));
            Assert.AreEqual(0, RuntimeSegmentDisplayRenderer.SourceIndex(new FaceRuntimeSevenSegmentDisplayManifestEntry(), 0, "Impact"));
        }

        [Test]
        public void PcmTransferBoundsLatencyAndClearsUnderflow()
        {
            var ring = new FabricPcmBuffer(2); ring.Write(new short[] { 1, 2, short.MinValue, short.MaxValue }, 4);
            var output = new float[4]; ring.Read(output);
            Assert.AreEqual(-1, output[0]); Assert.That(output[1], Is.EqualTo(32767f / 32768)); Assert.AreEqual(0, output[2]); Assert.AreEqual(0, output[3]);
            ring.Write(new short[] { 42 }, 1); ring.Clear(); ring.Read(output); CollectionAssert.AreEqual(new float[4], output);
        }
        private static async Task Until(Func<bool> ready)
        {
            for (var i = 0; i < 100 && !ready(); i++) await Task.Delay(10);
            Assert.True(ready(), "Worker did not reach expected state within one second.");
        }
        private sealed class FakeRuntime : IFabricRuntimeLibrary
        {
            public FakeSession Session = new FakeSession(); public bool Disposed;
            public IFabricMachineSession CreateSession(FabricLaunchRequest request) { return Session; }
            public void Dispose() { Disposed = true; }
        }
        private sealed class FakeSession : IFabricMachineSession
        {
            public bool Disposed, Stopped; public volatile bool Fail; public int Released; private ulong _sequence;
            public FabricCapabilities Capabilities { get { return new FabricCapabilities((ulong)(FabricCapability.Audio | FabricCapability.DigitalInput | FabricCapability.CoinInput)); } }
            public void Initialise() { }
            public void Reset() { }
            public void Advance(ulong nanoseconds) { if (Fail) throw new InvalidOperationException("native test failure"); if (nanoseconds != 1_000_000) throw new InvalidOperationException("Unexpected step"); }
            public FabricResult SubmitInput(FabricInput input) { if (!input.Active) Released++; return input.Kind == FabricInputKind.Coin ? FabricResult.InputRejected : FabricResult.Ok; }
            public FabricMachineSnapshot GetSnapshot() { return new FabricMachineSnapshot(++_sequence, Array.Empty<FabricLamp>(), Array.Empty<FabricReel>(), Array.Empty<FabricCharacterDisplay>(), Array.Empty<FabricSegmentDisplay>()); }
            public FabricAudioFormat GetAudioFormat() { return new FabricAudioFormat(48000, 2, 16, true, true, true); }
            public int ReadAudio(Span<short> samples, int frameCapacity) { samples[0] = 1; samples[1] = 2; return 1; }
            public void Shutdown() { Stopped = true; }
            public void Dispose() { Disposed = true; }
        }
    }
}
