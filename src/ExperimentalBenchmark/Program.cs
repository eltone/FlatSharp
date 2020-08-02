/*
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

namespace BenchmarkCore
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using benchfb;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using FlatSharp;
    using FlatSharp.Attributes;
    using FlatSharp.Unsafe;

    [ShortRunJob]
    public class Program
    {
        private Dictionary<SharedString, SharedString> hashtable;
        private SharedString[] array;
        private LinkedList<SharedString> linkedList;

        public static void Main(string[] args)
        { 
            BenchmarkRunner.Run<Program>();
        }

        [Params(1, 2, 4, 8)]
        public int ArrayLength { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.array = new SharedString[ArrayLength];
            this.hashtable = new Dictionary<SharedString, SharedString>(ArrayLength);
            this.linkedList = new LinkedList<SharedString>();

            for (int i = 0; i < ArrayLength; ++i)
            {
                string guid = Guid.NewGuid().ToString();
                this.array[i] = guid;
                this.hashtable[guid] = guid;
                this.linkedList.AddLast(guid);
            }
        }

        [Benchmark]
        public void Search_HashTable()
        {
            var array = this.array;
            var ht = this.hashtable;
            for (int i = 0; i < array.Length; ++i)
            {
                ht.TryGetValue(array[i], out var str);
            }
        }

        [Benchmark]
        public void Search_Array()
        {
            var array = this.array;
            for (int i = 0; i < array.Length; ++i)
            {
                string needle = array[i];
                for (int j = 0; j < array.Length; ++j)
                {
                    if (array[j] == needle)
                    {
                        break;
                    }
                }
            }
        }


        [Benchmark]
        public void Search_LinkedList()
        {
            var array = this.array;
            var linkedList = this.linkedList;
            for (int i = 0; i < array.Length; ++i)
            {
                var head = linkedList.First;
                string needle = array[i];
                while (head != null)
                {
                    if (head.Value == needle)
                    {
                        break;
                    }

                    head = head.Next;
                }
            }
        }

        [FlatBufferTable]
        public class SimpleVector<T>
        {
            [FlatBufferItem(0)]
            public virtual IList<T> Items { get; set; }
        }

        [FlatBufferTable]
        public class SortedVector<T>
        {
            [FlatBufferItem(0, SortedVector = true)]
            public virtual IList<SortedVectorItem<T>> Item { get; set; }
        }

        [FlatBufferTable]
        public class UnsortedVector<T>
        {
            [FlatBufferItem(0, SortedVector = false)]
            public virtual IList<SortedVectorItem<T>> Item { get; set; }
        }

        [FlatBufferTable]
        public class SortedVectorItem<T>
        {
            [FlatBufferItem(0, Key = true)]
            public virtual T Item { get; set; }
        }

        public static int NextGaussianInt(Random r, int min, int max)
        {
            double random = NextGaussian(r, (max - min) / 2.0, (max - min) / 6.0); // from -2 to 2.
            return Math.Max(min, Math.Min(max, (int)random));
        }

        public static double NextGaussian(Random r, double mu = 0, double sigma = 1)
        {
            var u1 = r.NextDouble();
            var u2 = r.NextDouble();

            var rand_std_normal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                Math.Sin(2.0 * Math.PI * u2);

            var rand_normal = mu + sigma * rand_std_normal;

            return rand_normal;
        }
    }
}
