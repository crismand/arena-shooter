using System.Collections.Generic;
using AS.Core;
using NUnit.Framework;

namespace AS.Tests
{
    public class EventBusTests
    {
        readonly struct Ping
        {
            public readonly int Value;
            public Ping(int value) { Value = value; }
        }

        readonly struct Pong { }

        [SetUp, TearDown]
        public void ResetBus() => EventBus.Clear();

        [Test]
        public void Publish_CallsEverySubscriber_WithThePayload()
        {
            var received = new List<int>();
            EventBus.Subscribe<Ping>(p => received.Add(p.Value));
            EventBus.Subscribe<Ping>(p => received.Add(p.Value * 10));

            EventBus.Publish(new Ping(3));

            CollectionAssert.AreEqual(new[] { 3, 30 }, received);
        }

        [Test]
        public void Unsubscribe_StopsDelivery_ButOtherListenersStillHear()
        {
            int first = 0, second = 0;
            void First(Ping _) => first++;
            void Second(Ping _) => second++;
            EventBus.Subscribe<Ping>(First);
            EventBus.Subscribe<Ping>(Second);

            EventBus.Unsubscribe<Ping>(First);
            EventBus.Publish(new Ping());

            Assert.AreEqual(0, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void Unsubscribe_TheLastListener_ThenPublish_DoesNothing()
        {
            int calls = 0;
            void Handler(Ping _) => calls++;
            EventBus.Subscribe<Ping>(Handler);
            EventBus.Unsubscribe<Ping>(Handler);

            Assert.DoesNotThrow(() => EventBus.Publish(new Ping()));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Unsubscribe_SomethingNeverSubscribed_IsHarmless()
        {
            Assert.DoesNotThrow(() => EventBus.Unsubscribe<Ping>(_ => { }));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new Ping(1)));
        }

        [Test]
        public void Events_AreRoutedByType()
        {
            int pings = 0, pongs = 0;
            EventBus.Subscribe<Ping>(_ => pings++);
            EventBus.Subscribe<Pong>(_ => pongs++);

            EventBus.Publish(new Pong());

            Assert.AreEqual(0, pings);
            Assert.AreEqual(1, pongs);
        }

        [Test]
        public void Clear_RemovesEverySubscriber()
        {
            int calls = 0;
            EventBus.Subscribe<Ping>(_ => calls++);

            EventBus.Clear();
            EventBus.Publish(new Ping());

            Assert.AreEqual(0, calls);
        }
    }
}
