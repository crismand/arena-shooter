using System.Collections.Generic;
using AS.Core;
using NUnit.Framework;
using UnityEngine;

namespace AS.Tests
{
    public class PrefabPoolTests
    {
        readonly List<GameObject> _created = new();
        Transform _prefab;
        Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _prefab = Track(new GameObject("PoolPrefab")).transform;
            _parent = Track(new GameObject("PoolParent")).transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        PrefabPool<Transform> NewPool() => new(_prefab, _parent);

        [Test]
        public void Get_PlacesTheItem_ThenActivatesIt_UnderTheParent()
        {
            var pool = NewPool();

            var item = pool.Get(new Vector3(3f, 4f, 0f), Quaternion.Euler(0f, 0f, 90f));
            Track(item.gameObject);

            Assert.IsTrue(item.gameObject.activeSelf);
            Assert.AreEqual(new Vector3(3f, 4f, 0f), item.position);
            Assert.AreEqual(90f, item.eulerAngles.z, 0.01f);
            Assert.AreSame(_parent, item.parent);
            Assert.AreEqual(1, pool.CountActive);
        }

        [Test]
        public void Release_DeactivatesTheItem_AndTheNextGetReusesIt()
        {
            var pool = NewPool();
            var first = pool.Get(Vector3.zero, Quaternion.identity);
            Track(first.gameObject);

            pool.Release(first);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(0, pool.CountActive);

            var second = pool.Get(Vector3.one, Quaternion.identity);
            Assert.AreSame(first, second, "A released item should be reused, not a new instance created.");
            Assert.AreEqual(Vector3.one, second.position);
        }

        [Test]
        public void Get_WhileItemsAreOut_CreatesNewInstances()
        {
            var pool = NewPool();
            var a = pool.Get(Vector3.zero, Quaternion.identity);
            var b = pool.Get(Vector3.zero, Quaternion.identity);
            Track(a.gameObject);
            Track(b.gameObject);

            Assert.AreNotSame(a, b);
            Assert.AreEqual(2, pool.CountActive);
        }
    }
}
