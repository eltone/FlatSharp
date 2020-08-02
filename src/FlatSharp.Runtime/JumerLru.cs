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
    using System.Runtime.CompilerServices;

    public class JumerLru<TKey, TValue> : ICache<TKey, TValue>
    {
        private readonly (TKey, TValue)?[] cache;

        public JumerLru(int capacity)
        {
            this.cache = new (TKey, TValue)?[capacity];
        }

        public IEnumerator<(TKey, TValue)> GetEnumerator()
        {
            var cache = this.cache;
            for (int i = 0; i < cache.Length; ++i)
            {
                var value = cache[i];
                if (value != null)
                {
                    yield return value.Value;
                }
            }
        }

        public (TKey, TValue)? Insert(TKey key, TValue item)
        {
            var cache = this.cache;
            int index = GetIndex(key, cache.Length);

            var value = cache[index];
            cache[index] = (key, item);
            return value;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            var cache = this.cache;
            int index = GetIndex(key, cache.Length);

            var existingValue = cache[index];
            if (existingValue != null)
            {
                var valueValue = existingValue.Value;
                if (valueValue.Item1.Equals(key))
                {
                    value = valueValue.Item2;
                    return true;
                }
            }

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(TKey key, int length)
        {
            return (int.MaxValue & key.GetHashCode()) % length;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
