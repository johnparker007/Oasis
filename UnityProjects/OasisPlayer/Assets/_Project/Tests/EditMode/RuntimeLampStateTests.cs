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
    }
}
