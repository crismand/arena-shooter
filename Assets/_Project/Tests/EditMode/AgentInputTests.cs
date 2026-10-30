using AS.Agents;
using NUnit.Framework;
using UnityEngine;

namespace AS.Tests
{
    public class AgentInputTests
    {
        GameObject _go;
        AgentInput _input;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AgentInput");
            _input = _go.AddComponent<AgentInput>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void Move_IsClampedToTheUnitCircle()
        {
            _input.Apply(new Vector2(1f, 1f), Vector2.zero, false);
            Assert.AreEqual(1f, _input.Move.magnitude, 1e-5f);
            Assert.AreEqual(_input.Move.x, _input.Move.y, 1e-5f);

            _input.Apply(new Vector2(0.3f, -0.2f), Vector2.zero, false);
            Assert.AreEqual(new Vector2(0.3f, -0.2f), _input.Move);
        }

        [Test]
        public void Aim_IsNormalised()
        {
            _input.Apply(Vector2.zero, new Vector2(0f, -0.5f), false);
            Assert.IsTrue(_input.TryGetAim(out var direction));
            Assert.AreEqual(Vector2.down, direction);
        }

        [Test]
        public void ANearZeroAim_KeepsThePreviousAim()
        {
            _input.Apply(Vector2.zero, Vector2.up, false);
            _input.Apply(Vector2.zero, new Vector2(0.05f, 0f), false);
            Assert.IsFalse(_input.TryGetAim(out var direction));
            Assert.AreEqual(Vector2.up, direction);
        }

        [Test]
        public void Fire_FollowsTheAction()
        {
            _input.Apply(Vector2.zero, Vector2.zero, true);
            Assert.IsTrue(_input.FireHeld);
            _input.Apply(Vector2.zero, Vector2.zero, false);
            Assert.IsFalse(_input.FireHeld);
        }

        [Test]
        public void Clear_StopsAndHoldsFire()
        {
            _input.Apply(Vector2.one, Vector2.left, true);
            _input.Clear();
            Assert.AreEqual(Vector2.zero, _input.Move);
            Assert.IsFalse(_input.FireHeld);
            Assert.IsFalse(_input.TryGetAim(out var direction));
            Assert.AreEqual(Vector2.left, direction);
        }
    }
}
