using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MfmeFmlDecoder.GameConfig;
using MfmeFmlDecoder.GameConfig.Structures;
using Xunit;

namespace MfmeFmlDecoder.src.GameConfig
{
    public class GameConfigDecoderTests
    {
        [Fact]
        public void ReelJumpersV1b_DecodesOutHiOnSlot2()
        {
            // Side B byte4 bit4 = hi for slot 2
            var tagB = new byte[8];
            tagB[4] = 0x10;
            string[] slots = ReelJumpersV1b.DecodeSlots(new byte[8], tagB);
            Assert.Equal(new[] { "out out", "out hi", "out out", "out out", "out out" }, slots);
        }

        [Fact]
        public void ReelJumpersV1b_ModeLabels()
        {
            Assert.Equal("V1b", ReelJumpersV1b.DecodeModeLabel(15, 1));
            Assert.Equal("V1b", ReelJumpersV1b.DecodeModeLabel(15, 2));
            Assert.Equal("V1a", ReelJumpersV1b.DecodeModeLabel(3, 1));
            Assert.Equal("Old1", ReelJumpersV1b.DecodeModeLabel(0, 1));
        }

        [Fact]
        public void GamFile_ParsesSystemAndDip()
        {
            const string text = "System MPU5\r\nDIP 1 10000000\r\nDIP 2 00000000\r\nEffects 255\r\n";
            GamFile gam = GamFile.Parse("test.gam", text);
            Assert.Equal("MPU5", gam.System);
            Assert.Equal("10000000", gam.DipBanks["DIP 1"]);
            Assert.True(gam.TryGetUInt("Effects", out uint effects));
            Assert.Equal(255u, effects);
        }

        [Fact]
        public void Decoder_EffectGrid_OmitsBlankRows()
        {
            var blob = new byte[16 * 8];
            // Triac 3 On = Meter (13), Triac 3 Off blank
            BitConverter.GetBytes(13u).CopyTo(blob, (3 - 1) * 8);

            string dir = Path.Combine(Path.GetTempPath(), "mfme-gc-fx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string gamPath = Path.Combine(dir, "t.gam");
                File.WriteAllText(gamPath, "System SCORPION5\nDIP 1 00000000\nDIP 2 00000000\nEffects 0\n");
                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    bw.Write(0x6Au);
                    bw.Write((uint)blob.Length);
                    bw.Write(blob);
                    bw.Write(0xFFFFFFFFu);
                    bw.Write(0u);
                }

                Assert.True(GameConfigDecoder.TryDecode(
                    Path.Combine(dir, "t.fml"), gamPath, ms.ToArray(), mapsDirectory: null,
                    out GameConfigDecodeResult result, out string skip), skip);

                using JsonDocument machine = JsonDocument.Parse(result.MachineJson);
                JsonElement fx = machine.RootElement.GetProperty("Settings").GetProperty("Triac Effects");
                Assert.Equal(1, fx.GetProperty("rows").GetArrayLength());
                Assert.Equal(3, fx.GetProperty("rows")[0].GetProperty("triac").GetInt32());
                Assert.Equal("Meter", fx.GetProperty("rows")[0].GetProperty("on").GetString());
                Assert.Equal("", fx.GetProperty("rows")[0].GetProperty("off").GetString());
                Assert.False(fx.TryGetProperty("tag", out _));
                Assert.False(fx.TryGetProperty("kind", out _));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Decoder_MeterGrid_MatchesMfmeInOutRows()
        {
            // 512-byte 0xA0: Sec 8 In=1, Sec 2 Out=0xFFFFFFFF (-1 literal),
            // Sec 3 Out=1, Sec 8 Out=1 — matches Cash King / MFME screenshot shape.
            var blob = new byte[512];
            void WriteU32(int offset, uint value) =>
                BitConverter.GetBytes(value).CopyTo(blob, offset);
            WriteU32(256 + (8 - 1) * 8 + 0, 1);
            WriteU32(256 + (2 - 1) * 8 + 4, unchecked((uint)(-1)));
            WriteU32(256 + (3 - 1) * 8 + 4, 1);
            WriteU32(256 + (8 - 1) * 8 + 4, 1);

            string dir = Path.Combine(Path.GetTempPath(), "mfme-gc-meters-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string gamPath = Path.Combine(dir, "t.gam");
                File.WriteAllText(gamPath, "System SCORPION5\nDIP 1 00000000\nDIP 2 00000000\nEffects 0\n");

                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    bw.Write(0xA0u);
                    bw.Write(512u);
                    bw.Write(blob);
                    bw.Write(0xFFFFFFFFu);
                    bw.Write(0u);
                }

                Assert.True(GameConfigDecoder.TryDecode(
                    Path.Combine(dir, "t.fml"),
                    gamPath,
                    ms.ToArray(),
                    mapsDirectory: null,
                    out GameConfigDecodeResult result,
                    out string skip), skip);

                using JsonDocument machine = JsonDocument.Parse(result.MachineJson);
                JsonElement meters = machine.RootElement.GetProperty("Settings").GetProperty("Meters");
                Assert.Equal(1, meters.GetProperty("in").GetArrayLength());
                Assert.Equal("Sec 8", meters.GetProperty("in")[0].GetProperty("source").GetString());
                Assert.Equal("1", meters.GetProperty("in")[0].GetProperty("multiplier").GetString());
                Assert.Equal(3, meters.GetProperty("out").GetArrayLength());
                Assert.Equal("Sec 2", meters.GetProperty("out")[0].GetProperty("source").GetString());
                Assert.Equal("-1", meters.GetProperty("out")[0].GetProperty("multiplier").GetString());
                Assert.Equal("Sec 3", meters.GetProperty("out")[1].GetProperty("source").GetString());
                Assert.Equal("1", meters.GetProperty("out")[1].GetProperty("multiplier").GetString());
                Assert.Equal("Sec 8", meters.GetProperty("out")[2].GetProperty("source").GetString());
                Assert.False(meters.TryGetProperty("tag", out _));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Decoder_HandlesScorpion5GamLinesAndWidthHeight()
        {
            string dir = Path.Combine(Path.GetTempPath(), "mfme-gc-s5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string gamPath = Path.Combine(dir, "t.gam");
                File.WriteAllText(gamPath,
                    "System SCORPION5\nDIP 1 00000000\nDIP 2 00000000\nPercentage 7\nSetPercent 84\nEffects 255\nStake 6\nJackpot 13\n");

                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    // 0x9D: screen1 600x800, screen2 480x640
                    bw.Write(0x9Du);
                    bw.Write(16u);
                    bw.Write(600u);
                    bw.Write(800u);
                    bw.Write(480u);
                    bw.Write(640u);
                    // Version split-ascii: 0x0D[0..8) + 0x9C[8..)
                    bw.Write(0x0Du);
                    bw.Write(8u);
                    bw.Write(Encoding.ASCII.GetBytes("Ver1.0\0\0"));
                    var tag9c = new byte[16];
                    Encoding.ASCII.GetBytes("tail\0").CopyTo(tag9c, 8);
                    bw.Write(0x9Cu);
                    bw.Write(16u);
                    bw.Write(tag9c);
                    bw.Write(0xFFFFFFFFu);
                    bw.Write(0u);
                }

                bool ok = GameConfigDecoder.TryDecode(
                    Path.Combine(dir, "t.fml"),
                    gamPath,
                    ms.ToArray(),
                    mapsDirectory: null,
                    out GameConfigDecodeResult result,
                    out string skip);
                Assert.True(ok, skip);

                using JsonDocument machine = JsonDocument.Parse(result.MachineJson);
                JsonElement s1 = machine.RootElement.GetProperty("Settings").GetProperty("Adder5 Screen 1");
                Assert.Equal("600 x 800 V", s1.GetString());
                JsonElement s2 = machine.RootElement.GetProperty("Settings").GetProperty("Adder5 Screen 2");
                Assert.Equal("480 x 600 V", s2.GetString());
                Assert.Equal("Ver1.0",
                    machine.RootElement.GetProperty("Settings").GetProperty("Version").GetString());

                using JsonDocument game = JsonDocument.Parse(result.GameJson);
                JsonElement pct = game.RootElement.GetProperty("Settings").GetProperty("Percentage");
                Assert.Equal("84%", pct.GetProperty("value").GetString());
                Assert.Equal(7, pct.GetProperty("raw").GetInt32());
                Assert.Equal(84, pct.GetProperty("also").GetProperty("setPercent").GetInt32());
                Assert.False(game.RootElement.GetProperty("Settings").GetProperty("WIP").GetProperty("value").GetBoolean());
                Assert.False(game.RootElement.GetProperty("Settings").GetProperty("Lo Tech").GetProperty("value").GetBoolean());
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void SiblingGamResolver_PrefersSameStem()
        {
            string dir = Path.Combine(Path.GetTempPath(), "mfme-gc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string fml = Path.Combine(dir, "Machine_[C01].fml");
                string gam = Path.Combine(dir, "Machine_[C01].gam");
                string other = Path.Combine(dir, "Other.gam");
                File.WriteAllText(fml, "");
                File.WriteAllText(gam, "System MPU5\n");
                File.WriteAllText(other, "System MPU5\n");
                string resolved = SiblingGamResolver.TryResolve(fml, out string reason);
                Assert.Null(reason);
                Assert.Equal(Path.GetFullPath(gam), resolved);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void FileLevelTagBag_SkipsStringTableAndFindsModeTag()
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                // 0x43 with empty value + one continuation string then next tag
                bw.Write(0x43u);
                bw.Write(0u); // length 0
                // continuation: length 5 "hi\0\0\0" wait - length includes null?
                // MeasureSpan: stringLength <= hostTagKeyByte (0x43). Use length 3 "ab\0"
                bw.Write(3u);
                bw.Write(Encoding.ASCII.GetBytes("ab\0"));
                // next tag 0x4F mode=15
                bw.Write(0x4Fu);
                bw.Write(4u);
                bw.Write(15u);
                bw.Write(0xFFFFFFFFu);
                bw.Write(0u);
            }

            Dictionary<uint, byte[]> tags = FileLevelTagBag.Parse(ms.ToArray());
            Assert.True(tags.ContainsKey(0x4F));
            Assert.Equal(15u, BitConverter.ToUInt32(tags[0x4F], 0));
        }

        [Fact]
        public void Decoder_SplitsMachineAndGame_FromMap()
        {
            Assert.True(
                GameConfigMapRegistry.TryGet("MPU5", out _, out string mapError),
                "MPU5 map must be registered: " + mapError);

            string dir = Path.Combine(Path.GetTempPath(), "mfme-gc-dec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string gamPath = Path.Combine(dir, "t.gam");
                File.WriteAllText(gamPath,
                    "System MPU5\nDIP 1 10000000\nDIP 2 00000000\nEffects 1\n");

                // Minimal FML plaintext: mode 15, RJ1 B slot2 hi, terminator
                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    bw.Write(0x4Fu);
                    bw.Write(4u);
                    bw.Write(15u);
                    var tag27 = new byte[8];
                    tag27[4] = 0x10;
                    bw.Write(0x27u);
                    bw.Write(8u);
                    bw.Write(tag27);
                    bw.Write(0x50u);
                    bw.Write(8u);
                    bw.Write(new byte[8]);
                    bw.Write(0x51u);
                    bw.Write(8u);
                    bw.Write(new byte[8]);
                    bw.Write(0xFFFFFFFFu);
                    bw.Write(0u);
                }

                bool ok = GameConfigDecoder.TryDecode(
                    Path.Combine(dir, "t.fml"),
                    gamPath,
                    ms.ToArray(),
                    mapsDirectory: null,
                    out GameConfigDecodeResult result,
                    out string skip);
                Assert.True(ok, skip);
                Assert.NotNull(result.MachineJson);
                Assert.NotNull(result.GameJson);

                using JsonDocument machine = JsonDocument.Parse(result.MachineJson);
                JsonElement rj1 = machine.RootElement.GetProperty("Settings").GetProperty("Reel Jumpers 1");
                Assert.Equal("V1b", rj1.GetProperty("value").GetString());
                Assert.Equal("out hi", rj1.GetProperty("slots")[1].GetString());
                Assert.False(rj1.TryGetProperty("tag", out _));
                Assert.False(rj1.TryGetProperty("kind", out _));
                Assert.False(rj1.TryGetProperty("raw", out _));

                using JsonDocument game = JsonDocument.Parse(result.GameJson);
                Assert.Equal("10000000",
                    game.RootElement.GetProperty("Settings").GetProperty("DIP")
                        .GetProperty("banks").GetProperty("DIP1").GetProperty("pattern").GetString());

                JsonElement dataPak = game.RootElement.GetProperty("Settings").GetProperty("DataPak");
                Assert.Equal("No", dataPak.GetProperty("value").GetString());
                Assert.Equal(0, dataPak.GetProperty("raw").GetInt32());
                Assert.True(dataPak.GetProperty("defaultUsed").GetBoolean());

                Assert.False(game.RootElement.GetProperty("Settings").GetProperty("Lo Tech").GetProperty("value").GetBoolean());
                Assert.True(game.RootElement.GetProperty("Settings").GetProperty("Lo Tech").GetProperty("defaultUsed").GetBoolean());
                Assert.False(game.RootElement.GetProperty("Settings").GetProperty("WIP").GetProperty("value").GetBoolean());
                Assert.Equal("", game.RootElement.GetProperty("Settings").GetProperty("Rating").GetProperty("value").GetString());
                Assert.Equal("", game.RootElement.GetProperty("Settings").GetProperty("Tags").GetProperty("value").GetString());
                // Effects 1 is present in the test GAM → Reels on, not a default.
                Assert.True(game.RootElement.GetProperty("Settings").GetProperty("EffectsReels").GetProperty("value").GetBoolean());
                Assert.False(game.RootElement.GetProperty("Settings").GetProperty("EffectsButtons").GetProperty("value").GetBoolean());
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Decoder_OutputMappings_DecodesFunctionLowParamHigh()
        {
            // Port 0 = Reel (14), port 1 = Meter + Value 2 (0x0202)
            var blob = new byte[1024];
            BitConverter.GetBytes((ushort)14).CopyTo(blob, 0);
            BitConverter.GetBytes((ushort)0x0202).CopyTo(blob, 2);

            string dir = Path.Combine(Path.GetTempPath(), "mfme-gc-om-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string gamPath = Path.Combine(dir, "t.gam");
                File.WriteAllText(gamPath, "System SRU\nDIP 1 00000000\nDIP 2 00000000\nEffects 0\n");
                using var ms = new MemoryStream();
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    bw.Write(0x9Cu);
                    bw.Write((uint)blob.Length);
                    bw.Write(blob);
                    bw.Write(0xFFFFFFFFu);
                    bw.Write(0u);
                }

                Assert.True(GameConfigDecoder.TryDecode(
                    Path.Combine(dir, "t.fml"), gamPath, ms.ToArray(), mapsDirectory: null,
                    out GameConfigDecodeResult result, out string skip), skip);

                using JsonDocument machine = JsonDocument.Parse(result.MachineJson);
                JsonElement entries = machine.RootElement
                    .GetProperty("Settings")
                    .GetProperty("Output Mappings")
                    .GetProperty("entries");
                Assert.Equal("Reel", entries[0].GetProperty("value").GetString());
                Assert.Equal(0, entries[0].GetProperty("param").GetInt32());
                Assert.Equal("Meter", entries[1].GetProperty("value").GetString());
                Assert.Equal(2, entries[1].GetProperty("param").GetInt32());
                Assert.Equal(0x0202, entries[1].GetProperty("raw").GetInt32());
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
