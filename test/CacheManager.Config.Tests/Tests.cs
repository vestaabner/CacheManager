using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheManager.Core;
using CacheManager.Core.Internal;

namespace CacheManager.Config.Tests
{
    public static class Tests
    {
        public static void CacheThreadTest(ICacheManager<string> cache, int seed)
        {
            cache.Clear();

            var threads = 10;
            var numItems = 1000;
            var eventAddCount = 0;
            var eventRemoveCount = 0;
            var eventGetCount = 0;

            cache.OnAdd += (sender, args) => { Interlocked.Increment(ref eventAddCount); };
            cache.OnRemove += (sender, args) => { Interlocked.Increment(ref eventRemoveCount); };
            cache.OnGet += (sender, args) => { Interlocked.Increment(ref eventGetCount); };

            Func<int, string> keyGet = (index) => "key" + ((index + 1) * seed);

            Action test = () =>
            {
                for (int i = 0; i < numItems; i++)
                {
                    cache.AddOrUpdate(keyGet(i), i.ToString(), _ => i.ToString() + "update");
                }

                for (int i = 0; i < numItems; i++)
                {
                    if (i % 10 == 0)
                    {
                        cache.Remove(keyGet(i));
                    }
                }

                for (int i = 0; i < numItems; i++)
                {
                    string val = cache.Get(keyGet(i));
                }
            };

            Parallel.Invoke(new ParallelOptions() { MaxDegreeOfParallelism = 8 }, Enumerable.Repeat(test, threads).ToArray());

            foreach (var handle in cache.CacheHandles)
            {
                var stats = handle.Stats;
                Console.WriteLine(string.Format(
                        "Items: {0}, Hits: {1}, Miss: {2}, Remove: {3}, ClearRegion: {4}, Clear: {5}, Adds: {6}, Puts: {7}, Gets: {8}",
                            stats.GetStatistic(CacheStatsCounterType.Items),
                            stats.GetStatistic(CacheStatsCounterType.Hits),
                            stats.GetStatistic(CacheStatsCounterType.Misses),
                            stats.GetStatistic(CacheStatsCounterType.RemoveCalls),
                            stats.GetStatistic(CacheStatsCounterType.ClearRegionCalls),
                            stats.GetStatistic(CacheStatsCounterType.ClearCalls),
                            stats.GetStatistic(CacheStatsCounterType.AddCalls),
                            stats.GetStatistic(CacheStatsCounterType.PutCalls),
                            stats.GetStatistic(CacheStatsCounterType.GetCalls)));
            }

            cache.Dispose();

            Console.WriteLine(string.Format(
                "Event - Adds {0} Hits {1} Removes {2}",
                eventAddCount,
                eventGetCount,
                eventRemoveCount));
        }

        public static void PutAndMultiGetTest(params ICacheManager<object>[] caches)
        {
            var swatch = Stopwatch.StartNew();
            var threads = 50;
            var items = 30000;
            long ops = 0;
            var misses = 0;
            var fails = 0;

            var rand = new Random();
            var key = "key";
            var region = "myRegion";

            foreach (var cache in caches)
            {
                for (var ta = 0; ta < items; ta++)
                {
                    Interlocked.Increment(ref ops);
                    cache.Put(key + ta, "val" + ta, region);
                    if (ta % 1000 == 0)
                    {
                        Console.Write(".");
                    }
                }

                var actions = new List<Action>();
                for (var t = 0; t < threads; t++)
                {
                    actions.Add(() =>
                    {
                        for (var ta = 0; ta < items; ta++)
                        {
                            Interlocked.Increment(ref ops);
                            var x = cache.Get(key + ta, region);

                            if (x == null)
                            {
                                Interlocked.Increment(ref misses);
                            }
                        }

                        object value;
                        Interlocked.Increment(ref ops);
                        Interlocked.Increment(ref ops); // update does a get and then set = 2 ops
                        if (!cache.TryUpdate("key" + rand.Next(0, items - 1), region, v => Guid.NewGuid().ToString(), out value))
                        {
                            Interlocked.Increment(ref fails);
                        }

                        Console.Write(".");
                    });
                }

                Parallel.Invoke(new ParallelOptions() { MaxDegreeOfParallelism = 8 }, actions.ToArray());
            }

            var elapsed = swatch.ElapsedMilliseconds;
            var opsPerSec = Math.Round(ops / swatch.Elapsed.TotalSeconds, 0);
            Console.WriteLine("\nPutAndMultiGetTest completed \tafter: {0:N} ms. \twith {1:N0} Ops/s \t{2} misses {3} fails", elapsed, opsPerSec, misses, fails);
        }

        public static void AddOrUpdateAndMultiGetTest(params ICacheManager<object>[] caches)
        {
            var swatch = Stopwatch.StartNew();
            var threads = 50;
            var items = 10000;
            long ops = 0;
            var misses = 0;
            var fails = 0;

            var rand = new Random();
            var key = "key";
            var region = "myRegion";

            foreach (var cache in caches)
            {
                for (var ta = 0; ta < items; ta++)
                {
                    Interlocked.Increment(ref ops);
                    Interlocked.Increment(ref ops);
                    Interlocked.Increment(ref ops);

                    var added = cache.AddOrUpdate(key + ta, region, "val" + ta, _ => "updated" + ta);
                    if (added == null)
                    {
                        throw new InvalidOperationException("AddOrUpdate shouldn't return null");
                    }

                    if (ta % 1000 == 0)
                    {
                        Console.Write(".");
                    }
                }

                var actions = new List<Action>();
                for (var t = 0; t < threads; t++)
                {
                    actions.Add(() =>
                    {
                        for (var ta = 0; ta < items; ta++)
                        {
                            Interlocked.Increment(ref ops);
                            var x = cache.Get(key + ta, region);

                            if (x == null)
                            {
                                Interlocked.Increment(ref misses);
                            }
                        }

                        object value;
                        Interlocked.Increment(ref ops);
                        Interlocked.Increment(ref ops); // update does a get and then set = 2 ops
                        if (!cache.TryUpdate("key" + rand.Next(0, items - 1), region, v => Guid.NewGuid().ToString(), out value))
                        {
                            Interlocked.Increment(ref fails);
                        }

                        Console.Write(".");
                    });
                }

                Parallel.Invoke(new ParallelOptions() { MaxDegreeOfParallelism = 8 }, actions.ToArray());
            }

            var elapsed = swatch.ElapsedMilliseconds;
            var opsPerSec = Math.Round(ops / swatch.Elapsed.TotalSeconds, 0);
            Console.WriteLine("\nSimpleAddGetTest completed \tafter: {0:N} ms. \twith {1:N0} Ops/s m:{2}\t f:{3}", elapsed, opsPerSec, misses, fails);
        }

        public static void AddThenGetAndUpdateTest(params ICacheManager<object>[] caches)
        {
            var swatch = Stopwatch.StartNew();
            var threads = 50;
            var items = 10000;
            var ops = 0;
            var misses = 0;
            var fails = 0;

            var rand = new Random();
            const string key = "key";
            const string region = "myRegion";

            foreach (var cache in caches)
            {
                for (var ta = 0; ta < items; ta++)
                {
                    Interlocked.Increment(ref ops);

                    cache.Add(key + ta, "val" + ta, region);

                    if (ta % 1000 == 0)
                    {
                        Console.Write(".");
                    }
                }

                var actions = new List<Action>();
                for (var t = 0; t < threads; t++)
                {
                    actions.Add(() =>
                    {
                        for (var ta = 0; ta < items; ta++)
                        {
                            Interlocked.Increment(ref ops);
                            var x = cache.Get(key + ta, region);

                            if (x == null)
                            {
                                Interlocked.Increment(ref misses);
                            }
                            if (ta % 1000 == 0)
                            {
                                object value;
                                Interlocked.Increment(ref ops);
                                Interlocked.Increment(ref ops); // update does a get and then set = 2 ops
                                if (!cache.TryUpdate("key" + rand.Next(0, items - 1), region, v => Guid.NewGuid().ToString(), out value))
                                {
                                    Interlocked.Increment(ref fails);
                                }
                            }
                        }

                        Console.Write(".");
                    });
                }

                Parallel.Invoke(new ParallelOptions() { MaxDegreeOfParallelism = 8 }, actions.ToArray());
            }

            var elapsed = swatch.ElapsedMilliseconds;
            var opsPerSec = Math.Round(ops / swatch.Elapsed.TotalSeconds, 0);
            Console.WriteLine("\nSimpleAddGetTest completed \tafter: {0:N} ms. \twith {1:N0} Ops/s m:{2}\t f:{3}", elapsed, opsPerSec, misses, fails);
        }

        public static void RandomRWTest(ICacheManager<Item> cache)
        {
            cache.Clear();

            const string keyPrefix = "RWKey_";
            const int actionsPerIteration = 104;
            const int initialLoad = 1000;
            int iterations = 0;
            int removeFails = 0;
            var keyIndex = 0;
            var random = new Random();

            Action create = () =>
            {
                if (keyIndex > 10000)
                {
                    keyIndex = initialLoad;
                }

                Interlocked.Increment(ref keyIndex);
                var key = keyPrefix + keyIndex;
                var newValue = Guid.NewGuid().ToString();
                var item = Item.Generate();

                cache.AddOrUpdate(key, item, p =>
                {
                    p.SomeStrings.Add(newValue);
                    return p;
                });
            };

            Action read = () =>
            {
                for (var i = 0; i < 100; i++)
                {
                    var val = cache.Get(keyPrefix + random.Next(1, keyIndex));
                }
            };

            Action remove = () =>
            {
                const int maxTries = 10;
                bool result = false;
                var tries = 0;
                string key;
                do
                {
                    tries++;
                    key = keyPrefix + random.Next(1, keyIndex);
                    result = cache.Remove(key);
                    if (!result)
                    {
                        Interlocked.Increment(ref removeFails);
                    }
                }
                while (!result && tries < maxTries);
            };

            Action report = () =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    Console.WriteLine(
                        "Index is at {0} Items in Cache: {1} failed removes {4} runs: {2} \t{3}",
                        keyIndex,
                        cache.CacheHandles.First().Count,
                        iterations,
                        iterations * actionsPerIteration,
                        removeFails);

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    foreach (var handle in cache.CacheHandles)
                    {
                        var stats = handle.Stats;
                        Console.WriteLine(string.Format(
                                "Items: {0}, Hits: {1}, Miss: {2}, Remove: {3} Adds: {4}",
                                    stats.GetStatistic(CacheStatsCounterType.Items),
                                    stats.GetStatistic(CacheStatsCounterType.Hits),
                                    stats.GetStatistic(CacheStatsCounterType.Misses),
                                    stats.GetStatistic(CacheStatsCounterType.RemoveCalls),
                                    stats.GetStatistic(CacheStatsCounterType.AddCalls)));
                    }

                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine();

                    iterations = 0;
                    removeFails = 0;
                }
            };

            for (var i = 0; i < initialLoad; i++)
            {
                create();
            }

            Task.Factory.StartNew(report);

            while (true)
            {
                try
                {
                    create();
                    create();
                    read();
                    remove();
                    remove();
                    iterations++;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message + "\n" + e.StackTrace);
                    Thread.Sleep(1000);
                }
            }
        }

        public static void TestEachMethod(ICacheManager<object> cache)
        {
            cache.Clear();

            cache.Add("key", "value", "region");
            cache.AddOrUpdate("key", "region", "value", _ => "update value", 22);

            cache.Expire("key", "region", TimeSpan.FromDays(1));
            var val = cache.Get("key", "region");
            var item = cache.GetCacheItem("key", "region");
            cache.Put("key", "put value");
            cache.RemoveExpiration("key");

            object update2;
            cache.TryUpdate("key", "region", _ => "update 2 value", out update2);

            object update3 = cache.Update("key", "region", _ => "update 3 value");

            cache.Remove("key", "region");

            cache.Clear();
            cache.ClearRegion("region");
        }
    }

    public class Item
    {
        private static Random random = new Random();

        public Item()
        {
            this.SomeStrings = new List<string>();
        }

        public string Name { get; set; }

        public IList<string> SomeStrings { get; set; }

        public long Number { get; set; }

        public static Item Generate()
        {
            return new Item()
            {
                Name = Guid.NewGuid().ToString(),
                Number = random.Next(0, int.MaxValue),
                SomeStrings = new List<string>() { "Something", "more", "or", "less" }
            };
        }
    }
}