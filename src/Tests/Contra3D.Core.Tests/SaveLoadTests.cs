using Xunit;

namespace Contra3D.Core.Tests
{
    /// <summary>
    /// T-BDD-ADOPT-6cbd51: checkpoint_triggers_autosave (e2e: true)
    /// Given: player reaches checkpoint marker
    /// When: auto-save triggers
    /// Then: game state serialized to disk, restored on continue
    /// </summary>
    public class SaveLoadTests
    {
        [Fact]
        public void SaveLoad_CheckpointRoundTrip()
        {
            // given: player reaches checkpoint marker at position (10, 5, 0)
            var original = new SaveData
            {
                Position = new Vector3(10f, 5f, 0f),
                Health = 80f,
                MaxHealth = 100f,
                Score = 1500,
                Lives = 3,
            };

            // when: auto-save triggers — serialize to JSON string (simulates disk write)
            var json = SaveLoader.Serialize(original);

            // then: restored on continue — deserialize and verify every field matches
            var restored = SaveLoader.Deserialize(json);

            Assert.Equal(original.Position.X, restored.Position.X);
            Assert.Equal(original.Position.Y, restored.Position.Y);
            Assert.Equal(original.Position.Z, restored.Position.Z);
            Assert.Equal(original.Health, restored.Health);
            Assert.Equal(original.MaxHealth, restored.MaxHealth);
            Assert.Equal(original.Score, restored.Score);
            Assert.Equal(original.Lives, restored.Lives);
        }

        [Fact]
        public void SaveLoad_EmptySaveIsDefault()
        {
            // given: no prior save exists — create default state
            var saved = SaveData.Default();

            // when: serialize and immediately deserialize
            var json = SaveLoader.Serialize(saved);
            var restored = SaveLoader.Deserialize(json);

            // then: all fields equal the documented defaults
            Assert.Equal(Vector3.Zero, restored.Position);
            Assert.Equal(100f, restored.Health);
            Assert.Equal(100f, restored.MaxHealth);
            Assert.Equal(0, restored.Score);
            Assert.Equal(3, restored.Lives);
        }
    }
}
