using System.Collections.Generic;
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
            Assert.That(settings.Graphics.BloomIntensity, Is.EqualTo(0.8f));
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
            settings.Graphics.BloomIntensity = 1.5f;
            Assert.That(store.Save(settings), Is.True);

            var loaded = store.Load();
            Assert.That(loaded.Graphics.LampExposureStops, Is.EqualTo(4.25f));
            Assert.That(loaded.Graphics.BloomEnabled, Is.False);
            Assert.That(loaded.Graphics.BloomIntensity, Is.EqualTo(1.5f));
        }

        [Test]
        public void InvalidValuesAreClamped()
        {
            File.WriteAllText(_path, "{\"Version\":1,\"Graphics\":{\"LampExposureStops\":99,\"BloomEnabled\":true,\"BloomIntensity\":-4}} ");
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

        [Test]
        public void UnknownPersistedValuesDoNotBlockKnownSettings()
        {
            File.WriteAllText(_path, "{\"Version\":99,\"Future\":{\"Ignored\":true},\"Graphics\":{\"LampExposureStops\":3.5,\"BloomEnabled\":false,\"BloomIntensity\":1.25}} ");
            var loaded = new PlayerSettingsStore(_path).Load();
            Assert.That(loaded.Graphics.LampExposureStops, Is.EqualTo(3.5f));
            Assert.That(loaded.Graphics.BloomEnabled, Is.False);
            Assert.That(loaded.Graphics.BloomIntensity, Is.EqualTo(1.25f));
        }

        [Test]
        public void PreviewNotifiesWithoutPersisting()
        {
            var store = new PlayerSettingsStore(_path);
            var service = new PlayerSettingsService(store);
            var notifications = new List<PlayerGraphicsSettings>();
            service.GraphicsChanged += g => notifications.Add(g);

            var preview = service.Graphics;
            preview.LampExposureStops = 4f;
            service.PreviewGraphics(preview);

            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(File.Exists(_path), Is.False);
        }

        [Test]
        public void ApplyPersistsCurrentValues()
        {
            var store = new PlayerSettingsStore(_path);
            var service = new PlayerSettingsService(store);
            var graphics = service.Graphics;
            graphics.LampExposureStops = 4f;

            Assert.That(service.ApplyGraphics(graphics), Is.True);
            Assert.That(store.Load().Graphics.LampExposureStops, Is.EqualTo(4f));
        }

        [Test]
        public void ControllerCancelRestoresOpeningSnapshot()
        {
            var service = new PlayerSettingsService(new PlayerSettingsStore(_path));
            var controller = new GraphicsSettingsTransaction(service);
            controller.Open();

            controller.SetLampExposure(5f);
            controller.Cancel();

            Assert.That(service.Graphics.LampExposureStops, Is.EqualTo(2.5f));
            Assert.That(File.Exists(_path), Is.False);
        }

        [Test]
        public void RestoreDefaultsPreviewsWithoutPersistence()
        {
            var settings = PlayerSettings.Defaults();
            settings.Graphics.LampExposureStops = 4f;
            var store = new PlayerSettingsStore(_path);
            store.Save(settings);
            var service = new PlayerSettingsService(store);
            var controller = new GraphicsSettingsTransaction(service);
            controller.Open();

            controller.SetLampExposure(5f);
            controller.RestoreDefaults();

            Assert.That(service.Graphics.LampExposureStops, Is.EqualTo(2.5f));
            Assert.That(store.Load().Graphics.LampExposureStops, Is.EqualTo(4f));
        }

        [Test]
        public void ApplyUpdatesOpeningSnapshotForLaterCancel()
        {
            var service = new PlayerSettingsService(new PlayerSettingsStore(_path));
            var controller = new GraphicsSettingsTransaction(service);
            controller.Open();

            controller.SetLampExposure(4f);
            controller.Apply();
            controller.SetLampExposure(6f);
            controller.Cancel();

            Assert.That(service.Graphics.LampExposureStops, Is.EqualTo(4f));
        }

        [Test]
        public void EscapeUsesCancelSemantics()
        {
            var service = new PlayerSettingsService(new PlayerSettingsStore(_path));
            var controller = new GraphicsSettingsTransaction(service);
            controller.Open();
            controller.SetLampExposure(6f);

            controller.Cancel();

            Assert.That(service.Graphics.LampExposureStops, Is.EqualTo(2.5f));
        }

        [Test]
        public void BloomIntensityIsFunctionallyDisabledWhenBloomIsOff()
        {
            var service = new PlayerSettingsService(new PlayerSettingsStore(_path));
            var controller = new GraphicsSettingsTransaction(service);
            controller.Open();

            controller.SetBloomEnabled(false);
            controller.SetBloomIntensity(1.9f);

            Assert.That(controller.IsBloomIntensityEnabled, Is.False);
            Assert.That(service.Graphics.BloomIntensity, Is.EqualTo(0.8f));
        }
    }
}
