﻿/*
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
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Defines a shared string writer, which can be used to 
    /// create shared strings within a flat buffer.
    /// </summary>
    public interface ISharedStringWriter
    {
        /// <summary>
        /// Writes a string at the given offset.
        /// </summary>
        void WriteString(
            SpanWriter writer,
            Span<byte> span,
            SharedString value,
            int offset,
            SerializationContext context);

        /// <summary>
        /// Flush any shared strings yet to be written.
        /// </summary>
        void FlushStrings(SpanWriter spanWriter, Span<byte> span, SerializationContext context);
    }

    /// <summary>
    /// LRU shared string writer.
    /// </summary>
    public class LruSharedStringWriter : ISharedStringWriter
    {
        private readonly LruManager manager;

        /// <summary>
        /// Defines a shared string writer with the given lookback capacity.
        /// </summary>
        public LruSharedStringWriter(int lookbackCapacity)
        {
            this.manager = new LruManager(lookbackCapacity);
        }

        /// <summary>
        /// Writes the given string to the buffer.
        /// </summary>
        public void WriteString(
            SpanWriter writer,
            Span<byte> span,
            SharedString value,
            int offset,
            SerializationContext context)
        {
            var tuple = this.manager.Add(value, offset);

            if (tuple != null)
            {
                (string str, LinkedList<int> list) = tuple.Value;
                Flush(str, list, writer, span, context);
            }
        }

        /// <summary>
        /// Flushes any pending strings.
        /// </summary>
        public void FlushStrings(
            SpanWriter spanWriter,
            Span<byte> span, 
            SerializationContext context)
        {
            foreach ((string str, LinkedList<int> offsets) in this.manager.GetNodes())
            {
                Flush(str, offsets, spanWriter, span, context);
            }

            this.manager.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Flush(
            SharedString value,
            LinkedList<int> offsetList,
            SpanWriter writer,
            Span<byte> span,
            SerializationContext context)
        {
            int stringOffset = writer.WriteAndProvisionString(span, value, context);

            LinkedListNode<int> node = offsetList.First;
            while (node != null)
            {
                writer.WriteUOffset(span, node.Value, stringOffset, context);
                node = node.Next;
            }
        }

        private class LruManager
        {
            private readonly Dictionary<SharedString, LinkedListNode<(SharedString str, LinkedList<int> offsets)>> indexMap;
            private readonly LinkedList<(SharedString str, LinkedList<int> offsets)> lruList;
            private int lookbackCount;

            public LruManager(int lookbackCapacity)
            {
                this.lookbackCount = lookbackCapacity;
                this.lruList = new LinkedList<(SharedString, LinkedList<int>)>();
                this.indexMap = new Dictionary<SharedString, LinkedListNode<(SharedString, LinkedList<int>)>>(lookbackCapacity);
            }

            public void Clear()
            {
                this.lruList.Clear();
                this.indexMap.Clear();
            }

            public IEnumerable<(SharedString, LinkedList<int>)> GetNodes()
            {
                var node = this.lruList.First;

                while (node != null)
                {
                    yield return node.Value;
                    node = node.Next;
                }
            }

            public (SharedString str, LinkedList<int>)? Add(SharedString @string, int offset)
            {
                var indexMap = this.indexMap;
                var lruList = this.lruList;

                if (indexMap.TryGetValue(@string, out var nodePair))
                {
                    nodePair.Value.offsets.AddLast(offset);
                    return null;
                }

                (SharedString, LinkedList<int>)? result = null;
                if (indexMap.Count >= lookbackCount)
                {
                    // find the last node.
                    var toEvict = lruList.Last;
                    lruList.RemoveLast();
                    indexMap.Remove(toEvict.Value.str);
                    result = toEvict.Value;
                }

                var offsetList = new LinkedList<int>();
                offsetList.AddFirst(offset);
                var lruNode = new LinkedListNode<(SharedString, LinkedList<int>)>((@string, offsetList));

                lruList.AddLast(lruNode);
                indexMap[@string] = lruNode;

                return result;
            }
        }
    }

    public class LinkedListStringWriter : ISharedStringWriter
    {
        private readonly int capacity;
        private readonly LinkedList<(SharedString, LinkedList<int>)> list;

        public LinkedListStringWriter(int capacity)
        {
            this.capacity = capacity;
            this.list = new LinkedList<(SharedString, LinkedList<int>)>();
        }

        public void FlushStrings(SpanWriter spanWriter, Span<byte> span, SerializationContext context)
        {
            var head = this.list.First;
            while (head != null)
            {
                Flush(span, spanWriter, head.Value.Item1, head.Value.Item2, context);
                head = head.Next;
            }
        }

        public void WriteString(SpanWriter writer, Span<byte> span, SharedString value, int offset, SerializationContext context)
        {
            var list = this.list;

            // search
            {
                var node = list.Last;
                while (node != null)
                {
                    if (node.Value.Item1 == value)
                    {
                        list.Remove(node);
                        list.AddLast(node);
                        node.Value.Item2.AddLast(offset);
                        return;
                    }

                    node = node.Previous;
                }
            }

            // not found
            var offsetList = new LinkedList<int>();
            offsetList.AddLast(offset);
            list.AddLast((value, offsetList));

            if (list.Count > this.capacity)
            {
                var removed = list.First;
                list.RemoveFirst();
                Flush(span, writer, removed.Value.Item1, removed.Value.Item2, context);
            }
        }

        private static void Flush(Span<byte> span, SpanWriter writer, SharedString value, LinkedList<int> offsets, SerializationContext context)
        {
            int stringOffset = writer.WriteAndProvisionString(span, value, context);
            var node = offsets.First;
            while (node != null)
            {
                writer.WriteUOffset(span, node.Value, stringOffset, context);
                node = node.Next;
            }
        }
    }
}