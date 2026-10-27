using AS.Core;
using NUnit.Framework;

namespace AS.Tests
{
    /// <summary>Runs against throwaway PlayerPrefs keys, so the real settings are never touched.</summary>
    public class GameSettingsTests
    {
        const string TestPrefix = "AS.Settings.EditModeTests.";

        [SetUp]
        public void SetUp()
        {
            GameSettings.KeyPrefix = TestPrefix;
            GameSettings.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.Clear();
            GameSettings.KeyPrefix = GameSettings.DefaultKeyPrefix;
        }

        [Test]
        public void Volumes_WithNothingSaved_AreFull()
        {
            Assert.AreEqual(1f, GameSettings.MusicVolume);
            Assert.AreEqual(1f, GameSettings.SfxVolume);
        }

        [Test]
        public void Volumes_AreStored_AndClampedToZeroToOne()
        {
            GameSettings.MusicVolume = 0.3f;
            GameSettings.SfxVolume = 2f;
            Assert.AreEqual(0.3f, GameSettings.MusicVolume, 0.0001f);
            Assert.AreEqual(1f, GameSettings.SfxVolume);

            GameSettings.SfxVolume = -1f;
            Assert.AreEqual(0f, GameSettings.SfxVolume);
        }

        [Test]
        public void Fullscreen_IsStored()
        {
            GameSettings.Fullscreen = true;
            Assert.IsTrue(GameSettings.Fullscreen);
            GameSettings.Fullscreen = false;
            Assert.IsFalse(GameSettings.Fullscreen);
        }

        [TestCase(1f, 0f)]
        [TestCase(0.5f, -6.02f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0f, GameSettings.SilentDecibels)]
        public void ToDecibels_MapsSliderValuesToMixerLevels(float volume, float decibels) =>
            Assert.AreEqual(decibels, GameSettings.ToDecibels(volume), 0.01f);
    }
}
