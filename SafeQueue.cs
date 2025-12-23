using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NT.Core.Net
{
    /// <summary>
    /// A thread safe Queue implementation. Though there is ConcurrentQueue in Net 4.x,
    /// it does not have TryDequeueAll method.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SafeQueue<T>
    {
        readonly Queue<T> _queue = new Queue<T>();
        /// <summary>
        /// Gets the approximate amount of items in the queue. Do NOT use Count to check
        /// if the queue is empty. The value may NOT be the same after the call. 
        /// </summary>
        /// <returns></returns>
        public int Count { get { lock (_queue) { return _queue.Count; } } }

        /// <summary>
        /// Add object to queue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            lock (_queue)
            {
                _queue.Enqueue(item);
            }
        }

        /// <summary>
        /// Tries to remove and return the object at the begining of the queue.
        /// </summary>
        /// <param name="result">
        /// When this method returns, if the operation was successful, result contains the object
        /// removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>true if an element was removed and returned successfully, otherwise false.</returns>
        public bool TryDequeue(out T result)
        {
            lock (_queue)
            {
                result = default(T);
                if (_queue.Count > 0)
                {
                    result = _queue.Dequeue();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Tries to remove and return all the objects in the queue.
        /// </summary>
        /// <param name="result">An array with all the objects in the queue.</param>
        /// <returns>true if there are objects removed, otherwise false</returns>
        public bool TryDequeueAll(out T[] result)
        {
            lock (_queue)
            {
                if (_queue.Count == 0)
                {
                    result = Array.Empty<T>();
                    return false;
                }
                result = _queue.ToArray();
                _queue.Clear();
                return true;
            }
        }

        /// <summary>
        /// Removes all objects from the queue.
        /// </summary>
        public void Clear()
        {
            lock (_queue)
            {
                _queue.Clear();
            }
        }
    }
}
