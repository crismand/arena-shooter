using AS.Pickups;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AS.Tests
{
    /// <summary>Which power-up a drop turns into, by drop weight.</summary>
    public class DropWeightTests
    {
        PowerUp _a, _never, _c;

        static PowerUp Make(float weight)
        {
            var powerUp = ScriptableObject.CreateInstance<ShieldPowerUp>();
            var so = new SerializedObject(powerUp);
            so.FindProperty("dropWeight").floatValue = weight;
            so.ApplyModifiedPropertiesWithoutUndo();
            return powerUp;
        }

        [SetUp]
        public void SetUp()
        {
            _a = Make(1f);
            _never = Make(0f);
            _c = Make(3f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_never);
            Object.DestroyImmediate(_c);
        }

        // Weights 1, 0, 3 split the roll at 0.25: below goes to the first, the rest to the last.
        [TestCase(0f, 0)]
        [TestCase(0.2f, 0)]
        [TestCase(0.3f, 2)]
        [TestCase(0.99f, 2)]
        [TestCase(1f, 2)]
        public void Roll_LandsInEachShare_InProportionToItsWeight(float roll, int expected)
        {
            var choices = new[] { _a, _never, _c };
            Assert.AreSame(choices[expected], PickupDropper.PickWeighted(choices, roll));
        }

        [Test]
        public void ZeroWeight_IsNeverPicked()
        {
            var choices = new[] { _never, _a };
            for (float roll = 0f; roll <= 1f; roll += 0.05f)
                Assert.AreSame(_a, PickupDropper.PickWeighted(choices, roll));
        }

        [Test]
        public void AllZeroWeights_PickNothing() =>
            Assert.IsNull(PickupDropper.PickWeighted(new[] { _never }, 0.5f));
    }
}
