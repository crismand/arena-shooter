using System.Collections.Generic;
using AS.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AS.Tests
{
    /// <summary>Arena event channels: forwarding to the global bus, and isolation between arenas.</summary>
    public class ArenaEventsTests
    {
        readonly struct Ping { }

        readonly List<GameObject> _created = new();

        [SetUp]
        public void ResetBus() => EventBus.Clear();

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _created) Object.DestroyImmediate(go);
            _created.Clear();
            EventBus.Clear();
        }

        [Test]
        public void ForwardingChannel_RunsItsOwnListenersFirst_ThenTheTarget()
        {
            var order = new List<string>();
            var global = new EventChannel();
            var local = new EventChannel(forwardTo: global);
            global.Subscribe<Ping>(_ => order.Add("global"));
            local.Subscribe<Ping>(_ => order.Add("local"));

            local.Publish(new Ping());

            CollectionAssert.AreEqual(new[] { "local", "global" }, order);
        }

        [Test]
        public void PublishingOnTheTarget_DoesNotReachTheForwardingChannel()
        {
            var global = new EventChannel();
            var local = new EventChannel(forwardTo: global);
            int localCalls = 0;
            local.Subscribe<Ping>(_ => localCalls++);

            global.Publish(new Ping());

            Assert.AreEqual(0, localCalls);
        }

        [Test]
        public void EventsFor_AChildOfAnArena_IsThatArenasChannel()
        {
            var arena = CreateArena("Arena", forward: true);
            var child = new GameObject("Child").transform;
            child.SetParent(arena.transform);

            Assert.AreSame(arena.Events, Arena.EventsFor(child));
        }

        [Test]
        public void EventsFor_SomethingOutsideAnyArena_IsTheGlobalChannel()
        {
            var loose = new GameObject("Loose");
            _created.Add(loose);

            Assert.AreSame(EventBus.Global, Arena.EventsFor(loose.transform));
        }

        [Test]
        public void TwoArenas_DontHearEachOther_AndOnlyTheForwardingOneReachesGlobal()
        {
            var watched = CreateArena("Watched", forward: true);
            var training = CreateArena("Training", forward: false);
            int watchedCalls = 0, trainingCalls = 0, globalCalls = 0;
            watched.Events.Subscribe<Ping>(_ => watchedCalls++);
            training.Events.Subscribe<Ping>(_ => trainingCalls++);
            EventBus.Subscribe<Ping>(_ => globalCalls++);

            training.Events.Publish(new Ping());
            Assert.AreEqual((0, 1, 0), (watchedCalls, trainingCalls, globalCalls));

            watched.Events.Publish(new Ping());
            Assert.AreEqual((1, 1, 1), (watchedCalls, trainingCalls, globalCalls));
        }

        Arena CreateArena(string name, bool forward)
        {
            var go = new GameObject(name);
            _created.Add(go);
            var arena = go.AddComponent<Arena>();
            var so = new SerializedObject(arena);
            so.FindProperty("forwardToGlobal").boolValue = forward;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arena;
        }
    }
}
