using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace AS.Core
{
    /// <summary>
    /// Pool of prefab instances. Items are positioned before they are activated, so physics
    /// bodies never start a frame at their previous location.
    /// </summary>
    public sealed class PrefabPool<T> where T : Component
    {
        readonly ObjectPool<T> _pool;
        // ObjectPool can't list what's out, and ReleaseAll needs to.
        readonly HashSet<T> _active = new();
        readonly List<T> _releasing = new();

        public PrefabPool(T prefab, Transform parent = null, int defaultCapacity = 32, int maxSize = 512)
        {
            _pool = new ObjectPool<T>(
                createFunc: () =>
                {
                    var item = Object.Instantiate(prefab, parent);
                    item.gameObject.SetActive(false);
                    return item;
                },
                actionOnRelease: item => item.gameObject.SetActive(false),
                actionOnDestroy: item => { if (item != null) Object.Destroy(item.gameObject); },
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public int CountActive => _pool.CountActive;

        public T Get(Vector3 position, Quaternion rotation)
        {
            var item = _pool.Get();
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            _active.Add(item);
            return item;
        }

        public void Release(T item)
        {
            _active.Remove(item);
            _pool.Release(item);
        }

        /// <summary>Returns every item that's out. Items destroyed from outside the pool are just forgotten.</summary>
        public void ReleaseAll()
        {
            _releasing.Clear();
            _releasing.AddRange(_active);
            _active.Clear();
            foreach (var item in _releasing)
                if (item != null) _pool.Release(item);
            _releasing.Clear();
        }
    }
}
