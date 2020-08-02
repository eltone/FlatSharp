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
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;

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

    public class SharedStringWriter : ISharedStringWriter
    {
        private readonly ICache<string, LinkedList<int>> cache;

        public SharedStringWriter(ICache<string, LinkedList<int>> cache)
        {
            this.cache = cache;
        }

        public void WriteString(
            SpanWriter writer,
            Span<byte> span,
            SharedString value,
            int offset,
            SerializationContext context)
        {
            var cache = this.cache;

            if (cache.TryGet(value, out LinkedList<int> offsetList))
            {
                offsetList.AddLast(offset);
                return;
            }

            offsetList = new LinkedList<int>();
            offsetList.AddFirst(offset);
            var tuple = cache.Insert(value, offsetList);
            if (tuple != null)
            {
                Flush(tuple.Value.Item1,
                    tuple.Value.Item2,
                    writer, span,
                    context);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Flush(
            string value,
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

        public void FlushStrings(SpanWriter spanWriter, Span<byte> span, SerializationContext context)
        {
            foreach (var item in this.cache)
            {
                Flush(item.Item1, item.Item2, spanWriter, span, context);
            }
        }
    }
}