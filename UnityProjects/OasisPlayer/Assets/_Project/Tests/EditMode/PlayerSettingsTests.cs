using System.IO;
using NUnit.Framework;
using OasisPlayer.Settings;

namespace OasisPlayer.Tests
{
    public sealed class PlayerSettingsTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"oasis-player-settings-{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void DefaultsAreValid()
        {
            var settings = PlayerSettings.Defaults();
            settings.Validate();
            Assert.That(settings.Graphics.LampExposureStops, Is.EqualTo(2.5f));
            Assert.That(settings.Graphics.BloomEnabled, Is.True);
        }

        [Test]
        public void MissingFileFallsBackToDefaults()
        {
            var loaded = new PlayerSettingsStore(_path).Load();
            Assert.That(loaded.Graphics.LampExposureStops, Is.EqualTo(2.5f));
        }

        [Test]
        public void ValidSettingsRoundTrip()
        {
            var store = new PlayerSettingsStore(_path);
            var settings = PlayerSettings.Defaults();
            settings.Graphics.LampExposureStops = 4.25f;
            settings.Graphics.BloomEnabled = false;
            settings.Graphics.BloomIntensity = 3.5f;
            Assert.That(store.Save(settings), Is.True);

            var loaded = store.Load();
            Assert.That(loaded.Graphics.LampExposureStops, Is.EqualTo(4.25f));
            Assert.That(loaded.Graphics.BloomEnabled, Is.False);
            Assert.That(loaded.Graphics.BloomIntensity, Is.EqualTo(3.5f));
        }

        [Test]
        public void InvalidValuesAreClamped()
        {
            File.WriteAllText(_path, "{\"Version\":1,\"Graphics\":{\"LampExposureStops\":99,\"BloomEnabled\":true,\"BloomIntensity\":-4}}");
            var loaded = new PlayerSettingsStore(_path).Load();
            Assert.That(loaded.Graphics.LampExposureStops, Is.EqualTo(PlayerGraphicsSettings.MaxLampExposureStops));
            Assert.That(loaded.Graphics.BloomIntensity, Is.EqualTo(PlayerGraphicsSettings.MinBloomIntensity));
        }

        [Test]
        public void CorruptFileFallsBackToDefaults()
        {
            File.WriteAllText(_path, "not json");
            var loaded = new PlayerSettingsStore(_path).Load();
            Assert.That(loaded.Graphics.LampExposureStops, Is.EqualTo(2.5f));
        }
    }
}
