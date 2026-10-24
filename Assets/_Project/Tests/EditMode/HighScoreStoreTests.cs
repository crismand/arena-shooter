using AS.Core;
using NUnit.Framework;

namespace AS.Tests
{
    /// <summary>Runs against a throwaway PlayerPrefs key, so the real best score is never touched.</summary>
    public class HighScoreStoreTests
    {
        const string TestKey = "AS.HighScore.EditModeTests";

        [SetUp]
        public void SetUp()
        {
            HighScoreStore.Key = TestKey;
            HighScoreStore.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            HighScoreStore.Clear();
            HighScoreStore.Key = HighScoreStore.DefaultKey;
        }

        [Test]
        public void Load_WithNothingSaved_IsZero() => Assert.AreEqual(0, HighScoreStore.Load());

        [Test]
        public void TrySubmit_ABetterScore_SavesIt()
        {
            Assert.IsTrue(HighScoreStore.TrySubmit(120));
            Assert.AreEqual(120, HighScoreStore.Load());

            Assert.IsTrue(HighScoreStore.TrySubmit(150));
            Assert.AreEqual(150, HighScoreStore.Load());
        }

        [TestCase(80)]
        [TestCase(100)]
        public void TrySubmit_ALowerOrEqualScore_KeepsTheBest(int score)
        {
            HighScoreStore.TrySubmit(100);

            Assert.IsFalse(HighScoreStore.TrySubmit(score));
            Assert.AreEqual(100, HighScoreStore.Load());
        }

        [Test]
        public void TrySubmit_Zero_OnAFreshStore_IsNotARecord() => Assert.IsFalse(HighScoreStore.TrySubmit(0));

        [Test]
        public void Clear_ForgetsTheBest()
        {
            HighScoreStore.TrySubmit(50);
            HighScoreStore.Clear();
            Assert.AreEqual(0, HighScoreStore.Load());
        }
    }
}
