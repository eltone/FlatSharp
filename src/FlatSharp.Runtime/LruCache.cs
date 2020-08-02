/*
 * Copyright 2020 James Courtney
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace FlatSharp
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    public class LruCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private static readonly ThreadLocal<LinkedList<(TKey, TValue)>> NodePool = new ThreadLocal<LinkedList<(TKey, TValue)>>(
            () => new LinkedList<(TKey, TValue)>());

        private readonly int capacity;
        private Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> map;
        private LinkedList<(TKey key, TValue value)> linkedList;

        public LruCache(int capacity)
        {
            this.map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity + 1);
            this.linkedList = new LinkedList<(TKey, TValue)>();
            this.capacity = capacity;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (this.map.TryGetValue(key, out var node))
            {
                value = node.Value.value;
                var list = node.List;
                list.Remove(node);
                list.AddFirst(node);

                return true;
            }

            value = default;
            return false;
        }

        public (TKey, TValue)? Insert(TKey key, TValue item)
        {
            var nodePool = NodePool.Value;
            var linkedList = this.linkedList;
            var map = this.map;

            LinkedListNode<(TKey, TValue)> newNode;
            if (nodePool.Count > 0)
            {
                newNode = nodePool.First;
                nodePool.RemoveFirst();
                newNode.Value = (key, item);
            }
            else
            {
                newNode = new LinkedListNode<(TKey, TValue)>((key, item));
            }

            linkedList.AddFirst(newNode);
            map[key] = linkedList.First;

            if (map.Count > this.capacity)
            {
                // Pull the oldest
                var evictedNode = linkedList.Last;
                var evicted = evictedNode.Value;

                linkedList.RemoveLast();
                nodePool.AddFirst(evicted);

                map.Remove(evicted.key);

                return evicted;
            }

            return null;
        }

        public IEnumerator<(TKey, TValue)> GetEnumerator()
        {
            var nodePool = NodePool.Value;
            var list = this.linkedList;
            var head = list.First;
            
            while (head != null)
            {
                yield return head.Value;

                var oldHead = head;
                head = head.Next;

                list.Remove(oldHead);
                nodePool.AddLast(oldHead);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
