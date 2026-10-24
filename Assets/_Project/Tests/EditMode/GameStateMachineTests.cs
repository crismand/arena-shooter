using System.Collections.Generic;
using AS.Core;
using NUnit.Framework;

namespace AS.Tests
{
    public class GameStateMachineTests
    {
        readonly List<GameStateChanged> _changes = new();

        void Record(GameStateChanged evt) => _changes.Add(evt);

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _changes.Clear();
            EventBus.Subscribe<GameStateChanged>(Record);
        }

        [TearDown]
        public void TearDown() => EventBus.Clear();

        [Test]
        public void StartsInBoot()
        {
            var machine = new GameStateMachine();
            Assert.AreEqual(GameState.Boot, machine.Current);
            Assert.IsTrue(machine.Is(GameState.Boot));
            Assert.IsFalse(machine.Is(GameState.Playing));
        }

        [Test]
        public void ChangeState_Switches_AndAnnouncesPreviousAndCurrent()
        {
            var machine = new GameStateMachine();

            machine.ChangeState(GameState.Playing);
            machine.ChangeState(GameState.Paused);

            Assert.AreEqual(GameState.Paused, machine.Current);
            Assert.AreEqual(2, _changes.Count);
            Assert.AreEqual(GameState.Boot, _changes[0].Previous);
            Assert.AreEqual(GameState.Playing, _changes[0].Current);
            Assert.AreEqual(GameState.Playing, _changes[1].Previous);
            Assert.AreEqual(GameState.Paused, _changes[1].Current);
        }

        [Test]
        public void ChangeState_ToTheCurrentState_IsIgnored()
        {
            var machine = new GameStateMachine();
            machine.ChangeState(GameState.Playing);

            machine.ChangeState(GameState.Playing);

            Assert.AreEqual(1, _changes.Count);
        }
    }
}
