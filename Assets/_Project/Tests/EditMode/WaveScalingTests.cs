using AS.Waves;
using NUnit.Framework;

namespace AS.Tests
{
    /// <summary>The endless-mode formulas, with the director's default tuning (growth 0.25, decay 0.9, min 0.25 s).</summary>
    public class WaveScalingTests
    {
        const float Growth = 0.25f;
        const float Decay = 0.9f;
        const float MinInterval = 0.25f;

        [TestCase(6, 0, ExpectedResult = 6, TestName = "ScaledCount_AuthoredWave_IsUnchanged")]
        [TestCase(12, 1, ExpectedResult = 15, TestName = "ScaledCount_OneExtraWave_Adds25Percent")]
        [TestCase(2, 1, ExpectedResult = 3, TestName = "ScaledCount_RoundsUp")]
        [TestCase(4, 4, ExpectedResult = 8, TestName = "ScaledCount_FourExtraWaves_Doubles")]
        public int ScaledCount(int baseCount, int extraWaves) =>
            WaveDirector.ScaledCount(baseCount, extraWaves, Growth);

        [Test]
        public void ScaledCount_NeverShrinks_AsWavesGoOn()
        {
            int previous = 0;
            for (int extra = 0; extra < 30; extra++)
            {
                int count = WaveDirector.ScaledCount(2, extra, Growth);
                Assert.GreaterOrEqual(count, previous, $"extra wave {extra}");
                previous = count;
            }
        }

        [Test]
        public void ScaledInterval_AuthoredWave_IsUnchanged() =>
            Assert.AreEqual(0.6f, WaveDirector.ScaledInterval(0.6f, 0, Decay, MinInterval), 1e-5f);

        [Test]
        public void ScaledInterval_OneExtraWave_IsTenPercentShorter() =>
            Assert.AreEqual(0.54f, WaveDirector.ScaledInterval(0.6f, 1, Decay, MinInterval), 1e-5f);

        [Test]
        public void ScaledInterval_NeverDropsBelowTheMinimum() =>
            Assert.AreEqual(MinInterval, WaveDirector.ScaledInterval(0.6f, 100, Decay, MinInterval), 1e-5f);

        [Test]
        public void ScaledInterval_BaseAlreadyBelowMinimum_IsRaisedToIt() =>
            Assert.AreEqual(MinInterval, WaveDirector.ScaledInterval(0.1f, 0, Decay, MinInterval), 1e-5f);
    }
}
