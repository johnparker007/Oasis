using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using OasisPlayer.Fabric;

namespace OasisPlayer.Tests
{
    public sealed class FabricAbiTests
    {
        [Test]
        public void AbiVersionEnumsAndKnownLayoutsMatchFabricV4()
        {
            Assert.AreEqual(0x00040000u, FabricAbi.Version);
            Assert.AreEqual(0u, (uint)FabricInputKind.Digital);
            Assert.AreEqual(1u, (uint)FabricInputKind.Coin);
            Assert.AreEqual(1UL, (ulong)FabricCapability.DigitalInput);
            Assert.AreEqual(128UL, (ulong)FabricCapability.DotMatrixDisplays);
            if (IntPtr.Size != 8) Assert.Ignore("The published Fabric native layout assertions target x64.");

            var assembly = typeof(FabricRuntimeLibrary).Assembly;
            AssertLayout(assembly, "FabricLaunchRequestNative", 1208);
            AssertLayout(assembly, "FabricRomResourceNative", 40);
            AssertLayout(assembly, "FabricCapabilitiesNative", 48);
            AssertLayout(assembly, "FabricInputNative", 88);
            AssertOffset(assembly, "FabricInputNative", "NumericalIndex", 72);
            AssertOffset(assembly, "FabricInputNative", "Kind", 76);
            AssertOffset(assembly, "FabricInputNative", "Active", 80);
            AssertOffset(assembly, "FabricInputNative", "CoinChannel", 81);
            AssertOffset(assembly, "FabricInputNative", "CoinValue", 82);
            AssertLayout(assembly, "FabricLampNative", 84);
            AssertLayout(assembly, "FabricReelNative", 80);
            AssertLayout(assembly, "FabricCharacterDisplayNative", 164);
            AssertLayout(assembly, "FabricSegmentDisplayNative", 208);
            AssertLayout(assembly, "FabricDotMatrixDisplayNative", 2140);
            AssertLayout(assembly, "FabricMachineSnapshotNative", 96);
            AssertLayout(assembly, "FabricAudioFormatNative", 20);
            AssertOffset(assembly, "FabricLaunchRequestNative", "RomPaths", 1160);
            AssertOffset(assembly, "FabricLaunchRequestNative", "Configuration", 1176);
            AssertOffset(assembly, "FabricLaunchRequestNative", "Resources", 1192);
            AssertOffset(assembly, "FabricRomResourceNative", "Path", 16);
            AssertOffset(assembly, "FabricMachineSnapshotNative", "Lamps", 16);
            AssertOffset(assembly, "FabricMachineSnapshotNative", "DotMatrices", 80);
            AssertOffset(assembly, "FabricMachineSnapshotNative", "DotMatrixCapacity", 88);
            AssertOffset(assembly, "FabricMachineSnapshotNative", "DotMatrixCount", 92);
            AssertOffset(assembly, "FabricCharacterDisplayNative", "Brightness", 160);
            AssertOffset(assembly, "FabricDotMatrixDisplayNative", "Width", 72);
            AssertOffset(assembly, "FabricDotMatrixDisplayNative", "Dots", 88);
            AssertOffset(assembly, "FabricDotMatrixDisplayNative", "Brightness", 2136);
        }

        [Test]
        public void FixedIdentifierEncodingIsBoundedAndUtf8Safe()
        {
            var buffer = new byte[FabricAbi.IdentifierCapacity];
            unsafe
            {
                fixed (byte* pointer = buffer)
                {
                    FabricRuntimeLibrary.WriteFixed(new string('x', 63), pointer, buffer.Length);
                    Assert.AreEqual(0, buffer[63]);
                    AssertWriteRejected(pointer, buffer.Length, new string('x', 64));
                    AssertWriteRejected(pointer, buffer.Length, new string('é', 32));
                }
            }
        }

        [Test]
        public void LaunchRequestRejectsInvalidRomSlotsAndAcceptsContiguousSlots()
        {
            var directory = Path.Combine(Path.GetTempPath(), "fabric-abi-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var backend = Path.Combine(directory, "backend.dll");
            var rom = Path.Combine(directory, "rom.bin");
            File.WriteAllBytes(backend, new byte[] { 0 });
            File.WriteAllBytes(rom, new byte[] { 0 });
            try
            {
                var invalid = new FabricLaunchRequest("amber", "machine", backend,
                    new[] { new FabricRomResource(FabricRomRole.Program, 1, rom) });
                Assert.Throws<ArgumentException>(() => FabricRuntimeLibrary.Validate(invalid));

                var valid = new FabricLaunchRequest("amber", "machine", backend,
                    new[] { new FabricRomResource(FabricRomRole.Program, 0, rom), new FabricRomResource(FabricRomRole.Sound, 0, rom) });
                Assert.DoesNotThrow(() => FabricRuntimeLibrary.Validate(valid));
                var relative = new FabricLaunchRequest("amber", "machine", "backend.dll", Array.Empty<FabricRomResource>());
                Assert.Throws<ArgumentException>(() => FabricRuntimeLibrary.Validate(relative));
            }
            finally { Directory.Delete(directory, true); }
        }

        private static void AssertLayout(Assembly assembly, string name, int expected)
        {
            var type = assembly.GetType("OasisPlayer.Fabric." + name, true);
            Assert.AreEqual(expected, Marshal.SizeOf(type), name + " size");
        }

        private static void AssertOffset(Assembly assembly, string name, string field, int expected)
        {
            var type = assembly.GetType("OasisPlayer.Fabric." + name, true);
            Assert.AreEqual(expected, Marshal.OffsetOf(type, field).ToInt32(), name + "." + field + " offset");
        }

        private static unsafe void AssertWriteRejected(byte* pointer, int capacity, string value)
        {
            try { FabricRuntimeLibrary.WriteFixed(value, pointer, capacity); }
            catch (ArgumentException) { return; }
            Assert.Fail("Expected an over-capacity UTF-8 value to be rejected.");
        }
    }
}
