// T-FUNC-P2-AISPAWN-TEST — AISpawnConfig 单元测试。
// 覆盖 YAML load / validate 全部场景。

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Contra3D.Core;

namespace Contra3D.Core.Tests
{
    public class AISpawnConfigTests
    {
        // ---- Default config ----

        [Fact]
        public void Default_MaxNormal_Returns12()
        {
            var config = AISpawnConfig.Default;
            Assert.Equal(12, config.MaxNormal);
        }

        [Fact]
        public void Default_MaxRusher_Returns4()
        {
            var config = AISpawnConfig.Default;
            Assert.Equal(4, config.MaxRusher);
        }

        [Fact]
        public void Default_AntiDoorCampingDistance_Returns5()
        {
            var config = AISpawnConfig.Default;
            Assert.Equal(5f, config.AntiDoorCampingDistance);
        }

        [Fact]
        public void Default_Instance_IsNotSameReference()
        {
            var a = AISpawnConfig.Default;
            var b = AISpawnConfig.Default;
            Assert.NotSame(a, b);
        }

        // ---- LoadFromString: valid YAML ----

        [Fact]
        public void LoadFromString_FullYAML_ParsesAllFields()
        {
            string yaml = "max_normal: 20\nmax_rusher: 8\nanti_door_camping_distance: 10.5";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(20, config.MaxNormal);
            Assert.Equal(8, config.MaxRusher);
            Assert.Equal(10.5f, config.AntiDoorCampingDistance);
        }

        [Fact]
        public void LoadFromString_PartialYAML_UsesDefaults()
        {
            string yaml = "max_normal: 6";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(6, config.MaxNormal);
            Assert.Equal(4, config.MaxRusher);       // default
            Assert.Equal(5f, config.AntiDoorCampingDistance); // default
        }

        [Fact]
        public void LoadFromString_EmptyYAML_UsesAllDefaults()
        {
            var config = AISpawnConfig.LoadFromString("");
            Assert.Equal(12, config.MaxNormal);
            Assert.Equal(4, config.MaxRusher);
            Assert.Equal(5f, config.AntiDoorCampingDistance);
        }

        [Fact]
        public void LoadFromString_SingleField_OnlySetsThatField()
        {
            string yaml = "anti_door_camping_distance: 3.0";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(12, config.MaxNormal);
            Assert.Equal(4, config.MaxRusher);
            Assert.Equal(3.0f, config.AntiDoorCampingDistance);
        }

        // ---- LoadFromString: comments and whitespace ----

        [Fact]
        public void LoadFromString_WithComments_IgnoresComments()
        {
            string yaml = "# This is a comment\nmax_normal: 15\n# another comment\nmax_rusher: 5";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(15, config.MaxNormal);
            Assert.Equal(5, config.MaxRusher);
        }

        [Fact]
        public void LoadFromString_WithEmptyLines_SkipsEmptyLines()
        {
            string yaml = "\n\nmax_normal: 8\n\nmax_rusher: 3\n\n";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(8, config.MaxNormal);
            Assert.Equal(3, config.MaxRusher);
        }

        [Fact]
        public void LoadFromString_WithWhitespace_TrimmedCorrectly()
        {
            string yaml = "  max_normal:  10  \n  max_rusher :  4  ";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(10, config.MaxNormal);
            Assert.Equal(4, config.MaxRusher);
        }

        // ---- LoadFromString: field order independence ----

        [Fact]
        public void LoadFromString_ReversedFieldOrder_ParsesCorrectly()
        {
            string yaml = "anti_door_camping_distance: 7.0\nmax_rusher: 6\nmax_normal: 18";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(18, config.MaxNormal);
            Assert.Equal(6, config.MaxRusher);
            Assert.Equal(7.0f, config.AntiDoorCampingDistance);
        }

        [Fact]
        public void LoadFromString_AllFieldsOutOfOrder_ParsesCorrectly()
        {
            string yaml = "max_rusher: 2\nmax_normal: 10\nanti_door_camping_distance: 8.0";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(10, config.MaxNormal);
            Assert.Equal(2, config.MaxRusher);
            Assert.Equal(8.0f, config.AntiDoorCampingDistance);
        }

        // ---- LoadFromString: boundary and edge values ----

        [Fact]
        public void LoadFromString_ZeroValues_ThrowsValidationException()
        {
            string yaml = "max_normal: 0\nmax_rusher: 0\nanti_door_camping_distance: 0";
            var ex = Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                AISpawnConfig.LoadFromString(yaml));
            Assert.Contains("max_normal must be > 0", ex.Errors);
            Assert.Contains("max_rusher must be > 0", ex.Errors);
        }

        [Fact]
        public void LoadFromString_NegativeCampingDistance_Throws()
        {
            string yaml = "max_normal: 10\nmax_rusher: 3\nanti_door_camping_distance: -1.0";
            var ex = Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                AISpawnConfig.LoadFromString(yaml));
            Assert.Contains("anti_door_camping_distance must be >= 0", ex.Errors);
        }

        [Fact]
        public void LoadFromString_RusherExceedsNormal_Throws()
        {
            string yaml = "max_normal: 5\nmax_rusher: 10\nanti_door_camping_distance: 5";
            var ex = Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                AISpawnConfig.LoadFromString(yaml));
            Assert.Contains("max_rusher cannot exceed max_normal", ex.Errors);
        }

        [Fact]
        public void LoadFromString_MaxRusherEqualsMaxNormal_IsValid()
        {
            string yaml = "max_normal: 10\nmax_rusher: 10\nanti_door_camping_distance: 5";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(10, config.MaxNormal);
            Assert.Equal(10, config.MaxRusher);
        }

        [Fact]
        public void LoadFromString_AntiDoorCampingZero_IsValid()
        {
            string yaml = "max_normal: 10\nmax_rusher: 3\nanti_door_camping_distance: 0";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(0f, config.AntiDoorCampingDistance);
        }

        [Fact]
        public void LoadFromString_MaxValues_ParsesCorrectly()
        {
            string yaml = "max_normal: 999\nmax_rusher: 999\nanti_door_camping_distance: 999.9";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(999, config.MaxNormal);
            Assert.Equal(999, config.MaxRusher);
            Assert.Equal(999.9f, config.AntiDoorCampingDistance);
        }

        // ---- LoadFromString: unknown fields ignored ----

        [Fact]
        public void LoadFromString_UnknownFields_Ignored()
        {
            string yaml = "max_normal: 10\nmax_rusher: 3\nsome_unknown_field: true\nanti_door_camping_distance: 5";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(10, config.MaxNormal);
            Assert.Equal(3, config.MaxRusher);
            Assert.Equal(5f, config.AntiDoorCampingDistance);
        }

        // ---- LoadFromString: numeric parsing edge cases ----

        [Fact]
        public void LoadFromString_FloatParsing_FractionalValue()
        {
            string yaml = "max_normal: 10\nmax_rusher: 3\nanti_door_camping_distance: 5.75";
            var config = AISpawnConfig.LoadFromString(yaml);
            Assert.Equal(5.75f, config.AntiDoorCampingDistance);
        }

        [Fact]
        public void LoadFromString_InvalidNumber_FallsBackToDefault()
        {
            // Unparseable number falls back to 0 via int/float.TryParse out param
            string yaml = "max_normal: abc\nmax_rusher: 3\nanti_door_camping_distance: 5";
            var ex = Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                AISpawnConfig.LoadFromString(yaml));
            Assert.Contains("max_normal must be > 0", ex.Errors);
        }

        // ---- Load (file-based) ----

        [Fact]
        public void Load_NonExistentFile_ThrowsFileNotFoundException()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"aispawn_{Guid.NewGuid()}.yaml");
            var ex = Assert.Throws<FileNotFoundException>(() =>
                AISpawnConfig.Load(tempPath));
            Assert.Contains(tempPath, ex.Message);
        }

        [Fact]
        public void Load_ValidFile_ParsesCorrectly()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"aispawn_{Guid.NewGuid()}.yaml");
            try
            {
                string yaml = "max_normal: 25\nmax_rusher: 10\nanti_door_camping_distance: 8.0";
                File.WriteAllText(tempPath, yaml);
                var config = AISpawnConfig.Load(tempPath);
                Assert.Equal(25, config.MaxNormal);
                Assert.Equal(10, config.MaxRusher);
                Assert.Equal(8.0f, config.AntiDoorCampingDistance);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void Load_FileWithValidationErrors_ThrowsAISpawnConfigLoadException()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"aispawn_{Guid.NewGuid()}.yaml");
            try
            {
                File.WriteAllText(tempPath, "max_normal: 0\nmax_rusher: 5");
                Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                    AISpawnConfig.Load(tempPath));
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        // ---- AISpawnConfig.AISpawnConfigLoadException ----

        [Fact]
        public void AISpawnConfigLoadException_Errors_NotNull()
        {
            var errors = new List<string> { "error1", "error2" };
            var ex = new AISpawnConfig.AISpawnConfigLoadException(errors);
            Assert.NotNull(ex.Errors);
            Assert.Equal(2, ex.Errors.Count);
            Assert.Equal("error1", ex.Errors[0]);
            Assert.Equal("error2", ex.Errors[1]);
        }

        [Fact]
        public void AISpawnConfigLoadException_NullErrors_ThrowsNullReferenceException()
        {
            // Constructor calls base(BuildMessage(errors)) before null-check, so null causes NRE.
            Assert.Throws<NullReferenceException>(() => new AISpawnConfig.AISpawnConfigLoadException(null!));
        }

        [Fact]
        public void AISpawnConfigLoadException_MessageContainsAllErrors()
        {
            var errors = new List<string> { "err_a", "err_b" };
            var ex = new AISpawnConfig.AISpawnConfigLoadException(errors);
            Assert.Contains("err_a", ex.Message);
            Assert.Contains("err_b", ex.Message);
        }

        // ---- Validation: combined errors ----

        [Fact]
        public void LoadFromString_MultipleValidationErrors_ReportsAll()
        {
            string yaml = "max_normal: 0\nmax_rusher: 0\nanti_door_camping_distance: -1";
            var ex = Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                AISpawnConfig.LoadFromString(yaml));
            Assert.Equal(3, ex.Errors.Count);
        }

        [Fact]
        public void LoadFromString_MaxRusherGreaterThanMaxNormal_ThrowsSingleError()
        {
            string yaml = "max_normal: 3\nmax_rusher: 10\nanti_door_camping_distance: 5";
            var ex = Assert.Throws<AISpawnConfig.AISpawnConfigLoadException>(() =>
                AISpawnConfig.LoadFromString(yaml));
            Assert.Single(ex.Errors);
            Assert.Contains("max_rusher cannot exceed max_normal", ex.Errors[0]);
        }

        // ---- Instance isolation ----

        [Fact]
        public void LoadFromString_DifferentConfigs_AreIndependent()
        {
            var c1 = AISpawnConfig.LoadFromString("max_normal: 5\nmax_rusher: 2\nanti_door_camping_distance: 3");
            var c2 = AISpawnConfig.LoadFromString("max_normal: 30\nmax_rusher: 15\nanti_door_camping_distance: 10");
            Assert.NotEqual(c1.MaxNormal, c2.MaxNormal);
            Assert.NotEqual(c1.MaxRusher, c2.MaxRusher);
            Assert.NotEqual(c1.AntiDoorCampingDistance, c2.AntiDoorCampingDistance);
        }
    }
}
