﻿/*
 * Copyright 2018 James Courtney
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
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// A buffer for reading from memory.
    /// </summary>
    public abstract class InputBuffer
    {
        internal static readonly Encoding Encoding = new UTF8Encoding(false);
        internal const byte True = 1;
        internal const byte False = 0;

        private CacheEntry[] sharedStringCache;

        #region Defined Methods

        protected InputBuffer()
        {
            this.SetSharedStringCacheSize(1);
        }

        /// <summary>
        /// Sets the capacity of the shared string cache for this buffer.
        /// </summary>
        public void SetSharedStringCacheSize(int size)
        {
            this.sharedStringCache = new CacheEntry[size];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBool(int offset)
        {
            return this.ReadByte(offset) != False;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString(int offset)
        {
            checked
            {
                // Strings are stored by reference.
                offset += this.ReadUOffset(offset);
                return this.ReadStringFromUOffset(offset);
            }
        }

        public virtual SharedString ReadSharedString(int offset)
        {
            var cache = this.sharedStringCache;
            int uoffset = checked(offset + this.ReadUOffset(offset));

            // Unlike spanwriter, which sits inside a synchronous method,
            // InputBuffer may be used concurrently on the same buffer.
            // Coarse-grained locking isn't great on a critical path, but adding fine-grained
            // locking adds quite a bit of initialization time and more time
            // to try to grab the lock on the bucket.
            // TODO: consider benchmarking effects of TryEnter
            // where we just reparse the string if we fail to immediately
            // enter the lock.
            lock (cache)
            {
                // uoffset guaranteed to be aligned to a 4 byte boundary, so we can easily 
                // divide by 4 as a quick and dirty hash.
                ref CacheEntry cacheItem = ref cache[(uoffset >> 2) % cache.Length];
                ref int cacheOffset = ref cacheItem.Offset;

                if (cacheOffset == uoffset)
                {
                    return cacheItem.String;
                }

                SharedString readValue = SharedString.FromNonNullStr(this.ReadStringFromUOffset(uoffset));
                cacheOffset = uoffset;
                cacheItem.String = readValue;
                return readValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ReadStringFromUOffset(int uoffset)
        {
            checked
            {
                int numberOfBytes = (int)this.ReadUInt(uoffset);
                return this.ReadStringProtected(uoffset + sizeof(int), numberOfBytes, Encoding);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUOffset(int offset)
        {
            uint uoffset = this.ReadUInt(offset);
            if (uoffset < sizeof(uint))
            {
                throw new IndexOutOfRangeException($"Decoded uoffset_t had value less than {sizeof(uint)}. Value = {uoffset}");
            }

            if (uoffset > int.MaxValue)
            {
                throw new IndexOutOfRangeException($"Decoded uoffset_t had value larger than max of {int.MaxValue}. Value = {uoffset}");
            }

            return (int)uoffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAbsoluteTableFieldLocation(int tableOffset, int index)
        {
            checked
            {
                int vtableOffset = tableOffset - this.ReadInt(tableOffset);
                int vtableLength = this.ReadUShort(vtableOffset);

                // VTable structure:
                // ushort: vtable length
                // ushort: table length
                // ushort: index 0 offset
                // ushort: index 1 offset
                // etc
                if (vtableLength < 4)
                {
                    throw new IndexOutOfRangeException("VTable was not long enough to be valid.");
                }

                // the max index is ((vtableLength - 4) / 2) - 1
                if (index >= (vtableLength - 4) / 2)
                {
                    // Not present, return 0. 0 is an indication that that field is not present.
                    return 0;
                }

                ushort relativeOffset = this.ReadUShort(vtableOffset + 2 * (2 + index));
                if (relativeOffset == 0)
                {
                    return 0;
                }

                return tableOffset + relativeOffset;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> ReadByteMemoryBlock(
           int uoffset,
           int sizePerItem)
        {
            return this.ReadByteMemoryBlockImpl(
                uoffset,
                sizePerItem,
                this.GetByteMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> ReadByteReadOnlyMemoryBlock(
           int uoffset,
           int sizePerItem)
        {
            return this.ReadByteMemoryBlockImpl(
                uoffset,
                sizePerItem,
                this.GetReadOnlyByteMemory);
        }

        private T ReadByteMemoryBlockImpl<T>(int uoffset, int sizePerItem, Func<int, int, T> callback)
        {
            Debug.Assert(sizePerItem == 1);

            checked
            {
                // The local value stores a uoffset_t, so follow that now.
                uoffset = uoffset + this.ReadUOffset(uoffset);

                // Skip the first 4 bytes of the vector, which contains the length.
                return callback(
                    uoffset + sizeof(uint),
                    (int)this.ReadUInt(uoffset));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<T> ReadMemoryBlock<T>(
           int uoffset,
           int sizePerItem) where T : struct
        {
            return this.ReadMemoryBlockImpl<T>(uoffset, sizePerItem, (s, l) => this.GetByteMemory(s, l));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<T> ReadReadOnlyMemoryBlock<T>(
           int uoffset,
           int sizePerItem) where T : struct
        {
            return this.ReadMemoryBlockImpl<T>(uoffset, sizePerItem, this.GetReadOnlyByteMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Memory<T> ReadMemoryBlockImpl<T>(
           int uoffset,
           int sizePerItem,
           Func<int, int, ReadOnlyMemory<byte>> callback) where T : struct
        {
            checked
            {
                // The local value stores a uoffset_t, so follow that now.
                uoffset = uoffset + this.ReadUOffset(uoffset);

                ReadOnlyMemory<byte> innerMemory = callback(
                    uoffset + sizeof(uint),
                    ((int)this.ReadUInt(uoffset)) * sizePerItem);

                MemoryTypeChanger<T> typeChanger = new MemoryTypeChanger<T>(MemoryMarshal.AsMemory(innerMemory));
                return typeChanger.Memory;
            }
        }

        #endregion

        public abstract int Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract byte ReadByte(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract sbyte ReadSByte(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract ushort ReadUShort(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract short ReadShort(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract uint ReadUInt(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract int ReadInt(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract ulong ReadULong(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract long ReadLong(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract float ReadFloat(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public abstract double ReadDouble(int offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract string ReadStringProtected(int offset, int byteLength, Encoding encoding);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract Memory<byte> GetByteMemory(int start, int length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract ReadOnlyMemory<byte> GetReadOnlyByteMemory(int start, int length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        protected static void CheckAlignment(int offset, int size)
        {
            if (offset % size != 0)
            {
                throw new InvalidOperationException($"BugCheck: attempted to read unaligned data at index: {offset}, expected alignment: {size}");
            }
        }

        private struct CacheEntry
        {
            public int Offset;
            public SharedString String;
        }
    }
}
