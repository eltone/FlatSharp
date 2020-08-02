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

    public interface ICache<TKey, TValue> : IEnumerable<(TKey, TValue)>
    {
        /// <summary>
        /// Attempts to get a key from the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool TryGet(TKey key, out TValue value);

        /// <summary>
        /// Inserts the given key/value pair into the cache.
        /// </summary>
        /// <returns>The key/value pair that was evicted as a result of this operation. Null if no item was evicted.</returns>
        (TKey, TValue)? Insert(TKey key, TValue item);
    }
}
