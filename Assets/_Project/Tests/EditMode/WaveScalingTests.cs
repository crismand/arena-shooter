using AS.Waves;
using NUnit.Framework;

namespace AS.Tests
{
    /// <summary>
    /// The endless-mode formulas, with the director's default tuning (growth 0.25, decay 0.9, min 0.25 s),
    /// and where boss waves fall (every fifth wave by default).
    /// </summary>
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

        [TestCase(4, ExpectedResult = false)]
        [TestCase(5, ExpectedResult = true)]
        [TestCase(6, ExpectedResult = false)]
        [TestCase(10, ExpectedResult = true)]
        public bool IsBossWave_EveryFifth(int wave) => WaveDirector.IsBossWave(wave, 5);

        [Test]
        public void IsBossWave_ZeroTurnsBossesOff() => Assert.IsFalse(WaveDirector.IsBossWave(5, 0));

        // Boss waves slot in between, so every authored wave still plays: 6 and 7 are authored waves 5 and 6.
        [TestCase(4, ExpectedResult = 4)]
        [TestCase(6, ExpectedResult = 5)]
        [TestCase(7, ExpectedResult = 6)]
        [TestCase(11, ExpectedResult = 9)]
        public int NormalWaveNumber_SkipsBossWaves(int wave) => WaveDirector.NormalWaveNumber(wave, 5);

        [TestCase(1, ExpectedResult = 1f)]
        [TestCase(2, ExpectedResult = 1.5f)]
        [TestCase(3, ExpectedResult = 2f)]
        public float BossHealthMultiplier_GrowsWithEachBoss(int bossNumber) => WaveDirector.BossHealthMultiplier(bossNumber, 0.5f);
    }
}
