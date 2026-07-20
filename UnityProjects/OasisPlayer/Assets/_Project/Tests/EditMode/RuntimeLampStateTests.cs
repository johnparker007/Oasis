using NUnit.Framework;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Tests
{
    public sealed class RuntimeLampStateTests
    {
        [Test]
        public void LampBrightnessDefaultsToZeroAndSupportsSetGetClampAndClear()
        {
            var state = new RuntimeLampState();
            Assert.AreEqual(0f, state.GetBrightness(7));
            Assert.True(state.SetBrightness(7, 0.5f));
            Assert.AreEqual(0.5f, state.GetBrightness(7), 0.0001f);
            Assert.True(state.SetBrightness(7, 2f));
            Assert.AreEqual(1f, state.GetBrightness(7), 0.0001f);
            Assert.True(state.ClearAll());
            Assert.AreEqual(0f, state.GetBrightness(7), 0.0001f);
        }

        [Test]
        public void InvalidAndUnchangedLampValuesDoNotDirtyState()
        {
            var state = new RuntimeLampState();
            Assert.False(state.SetBrightness(0, 1f));
            Assert.False(state.SetBrightness(256, 1f));
            Assert.False(state.IsDirty);
            Assert.True(state.SetBrightness(1, 1f));
            state.MarkClean();
            Assert.False(state.SetBrightness(1, 1f));
            Assert.False(state.IsDirty);
        }

        [Test]
        public void TextureUploadCoalescesMultipleStateChanges()
        {
            var state = new RuntimeLampState();
            using (var texture = new RuntimeLampStateTexture(state))
            {
                var initialUploads = texture.UploadCount;
                state.SetBrightness(1, 0.25f);
                state.SetBrightness(2, 0.5f);
                Assert.True(texture.Upload(state));
                Assert.AreEqual(initialUploads + 1, texture.UploadCount);
                Assert.False(texture.Upload(state));
                Assert.AreEqual(initialUploads + 1, texture.UploadCount);
            }
        }

        [Test]
        public void LookupDecoderUsesOneBasedLampIdsAndThreeRgbContributions()
        {
            var brightness = new float[256];
            brightness[1] = 1f;
            brightness[2] = 0.5f;
            brightness[3] = 0.25f;
            var value = RuntimeFaceLampLookupDecoder.Accumulate(brightness, new[] { 1, 2, 0 }, new[] { 255, 128, 255 });
            Assert.AreEqual(1.25098f, value, 0.0002f);
            Assert.AreEqual(0, RuntimeFaceLampLookupDecoder.ResolveLampStateIndex(1, 0));
            Assert.AreEqual(7, RuntimeFaceLampLookupDecoder.ResolveLampStateIndex(99, 7));
        }

        [Test]
        public void SetAllBrightnessSetsValidLampsAndLeavesSentinelZeroWithoutRedirtyingUnchangedState()
        {
            var state = new RuntimeLampState();
            Assert.True(state.SetAllBrightness(1f));
            Assert.AreEqual(0f, state.GetBrightness(0));
            Assert.AreEqual(1f, state.GetBrightness(1));
            Assert.AreEqual(1f, state.GetBrightness(255));
            state.MarkClean();
            Assert.False(state.SetAllBrightness(1f));
            Assert.False(state.IsDirty);
            Assert.True(state.SetAllBrightness(0f));
            Assert.AreEqual(0f, state.GetBrightness(1));
            Assert.AreEqual(0f, state.GetBrightness(255));
        }

        [Test]
        public void AutomaticDiagnosticDefaultsAndClampsLampRange()
        {
            var defaults = RuntimeLampDiagnosticSettings.DefaultAutomatic();
            Assert.AreEqual(RuntimeLampDiagnosticMode.AutomaticSweep, defaults.Mode);
            Assert.AreEqual(1, defaults.FirstLamp);
            Assert.AreEqual(255, defaults.LastLamp);
            Assert.AreEqual(0.1f, defaults.SecondsPerLamp, 0.0001f);

            var clamped = new RuntimeLampDiagnosticSettings
            {
                Mode = RuntimeLampDiagnosticMode.AutomaticSweep,
                FirstLamp = 0,
                LastLamp = 999,
                SecondsPerLamp = 0f,
                AllOnSeconds = 0f,
                AllOffSeconds = 0f,
                AllFlashCount = 0,
                Repeat = true
            }.Clamped();

            Assert.AreEqual(1, clamped.FirstLamp);
            Assert.AreEqual(255, clamped.LastLamp);
            Assert.Greater(clamped.SecondsPerLamp, 0f);
            Assert.AreEqual(1, clamped.AllFlashCount);
        }

        [Test]
        public void AutomaticSequenceRunsTwoAllOnFlashesBeforeSweepAndRepeats()
        {
            var state = new RuntimeLampState();
            var settings = RuntimeLampDiagnosticSettings.DefaultAutomatic();
            settings.FirstLamp = 1;
            settings.LastLamp = 3;
            settings.SecondsPerLamp = 0.1f;
            settings.AllOnSeconds = 0.5f;
            settings.AllOffSeconds = 0.5f;
            settings.AllFlashCount = 2;
            settings.Repeat = true;
            var sequence = new RuntimeLampDiagnosticSequence(settings);

            Assert.True(sequence.Start(state));
            Assert.AreEqual(RuntimeLampDiagnosticStage.AllOn, sequence.CurrentStage);
            Assert.AreEqual(1f, state.GetBrightness(1));
            Assert.AreEqual(1f, state.GetBrightness(255));

            Assert.AreEqual(RuntimeLampDiagnosticTransition.AllOff, sequence.Advance(state, 0.5f));
            Assert.AreEqual(0f, state.GetBrightness(1));
            Assert.AreEqual(RuntimeLampDiagnosticTransition.AllOn, sequence.Advance(state, 0.5f));
            Assert.AreEqual(1f, state.GetBrightness(2));
            Assert.AreEqual(RuntimeLampDiagnosticTransition.AllOff, sequence.Advance(state, 0.5f));
            Assert.AreEqual(RuntimeLampDiagnosticTransition.BeginSweep, sequence.Advance(state, 0.5f));
            Assert.AreEqual(RuntimeLampDiagnosticStage.Sweep, sequence.CurrentStage);
            Assert.AreEqual(1, sequence.CurrentLamp);
            AssertOnlyLampEnabled(state, 1, 3, 1);

            Assert.AreEqual(RuntimeLampDiagnosticTransition.SweepLamp, sequence.Advance(state, 0.1f));
            Assert.AreEqual(2, sequence.CurrentLamp);
            AssertOnlyLampEnabled(state, 1, 3, 2);
            Assert.AreEqual(RuntimeLampDiagnosticTransition.SweepLamp, sequence.Advance(state, 0.1f));
            Assert.AreEqual(3, sequence.CurrentLamp);
            AssertOnlyLampEnabled(state, 1, 3, 3);
            Assert.AreEqual(RuntimeLampDiagnosticTransition.RepeatAllOn, sequence.Advance(state, 0.1f));
            Assert.AreEqual(1, sequence.CompletedCycles);
            Assert.AreEqual(RuntimeLampDiagnosticStage.AllOn, sequence.CurrentStage);
            Assert.AreEqual(1f, state.GetBrightness(1));
        }


        [Test]
        public void AutomaticSweepReachesLamp255()
        {
            var state = new RuntimeLampState();
            var settings = RuntimeLampDiagnosticSettings.DefaultAutomatic();
            settings.AllOnSeconds = 0.01f;
            settings.AllOffSeconds = 0.01f;
            settings.SecondsPerLamp = 0.1f;
            var sequence = new RuntimeLampDiagnosticSequence(settings);
            sequence.Start(state);
            sequence.Advance(state, 0.01f);
            sequence.Advance(state, 0.01f);
            sequence.Advance(state, 0.01f);
            sequence.Advance(state, 0.01f);

            Assert.AreEqual(RuntimeLampDiagnosticStage.Sweep, sequence.CurrentStage);
            Assert.AreEqual(1, sequence.CurrentLamp);
            for (var i = 2; i <= 255; i++)
            {
                sequence.Advance(state, 0.1f);
            }

            Assert.AreEqual(255, sequence.CurrentLamp);
            Assert.AreEqual(1f, state.GetBrightness(255));
            Assert.AreEqual(0f, state.GetBrightness(254));
        }

        [Test]
        public void AutomaticSequenceDoesNothingWhenOff()
        {
            var state = new RuntimeLampState();
            var settings = RuntimeLampDiagnosticSettings.DefaultAutomatic();
            settings.Mode = RuntimeLampDiagnosticMode.Off;
            var sequence = new RuntimeLampDiagnosticSequence(settings);

            Assert.False(sequence.Start(state));
            Assert.AreEqual(RuntimeLampDiagnosticTransition.None, sequence.Advance(state, 1f));
            Assert.AreEqual(0f, state.GetBrightness(1));
        }

        [Test]
        public void LookupDiagnosticReportsEmptyAndValidLookupData()
        {
            var emptyIds = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var emptyWeights = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var validIds = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            var validWeights = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);

            try
            {
                emptyIds.SetPixel(0, 0, Color.clear);
                emptyIds.Apply();
                emptyWeights.SetPixel(0, 0, Color.clear);
                emptyWeights.Apply();
                var emptyFace = CreateLookupFace(emptyIds, emptyWeights);
                var empty = RuntimeFaceLookupDiagnostic.Analyze(emptyFace);
                Assert.True(empty.HasLampIdData);
                Assert.True(empty.HasLampWeightData);
                Assert.AreEqual(0, empty.AssignedPixels);
                Assert.False(empty.HasNonZeroWeights);

                validIds.SetPixels32(new[] { new Color32(3, 0, 0, 255), new Color32(217, 5, 0, 255) });
                validIds.Apply();
                validWeights.SetPixels32(new[] { new Color32(128, 0, 0, 255), new Color32(255, 0, 0, 255) });
                validWeights.Apply();
                var validFace = CreateLookupFace(validIds, validWeights);
                var valid = RuntimeFaceLookupDiagnostic.Analyze(validFace);
                Assert.AreEqual(2, valid.AssignedPixels);
                Assert.AreEqual(3, valid.MinimumLampId);
                Assert.AreEqual(217, valid.MaximumLampId);
                Assert.AreEqual(0, valid.InvalidIdCount);
                Assert.True(valid.HasNonZeroWeights);
            }
            finally
            {
                Object.DestroyImmediate(emptyIds);
                Object.DestroyImmediate(emptyWeights);
                Object.DestroyImmediate(validIds);
                Object.DestroyImmediate(validWeights);
            }
        }

        private static void AssertOnlyLampEnabled(RuntimeLampState state, int firstLamp, int lastLamp, int expectedLamp)
        {
            for (var lamp = firstLamp; lamp <= lastLamp; lamp++)
            {
                Assert.AreEqual(lamp == expectedLamp ? 1f : 0f, state.GetBrightness(lamp), 0.0001f, $"Lamp {lamp}");
            }
        }

        private static RuntimeFace CreateLookupFace(Texture2D ids, Texture2D weights)
        {
            return new RuntimeFace(
                new MachineRuntimeFaceReference { faceId = "lookup", cabinetFaceTargetId = "front" },
                new FaceRuntimeManifest { schemaVersion = 2, faceId = "lookup", width = ids.width, height = ids.height },
                null,
                null,
                null,
                null,
                new RuntimeTextureAsset("lampIds0.png", ids),
                new RuntimeTextureAsset("lampWeights0.png", weights));
        }

    }
}
