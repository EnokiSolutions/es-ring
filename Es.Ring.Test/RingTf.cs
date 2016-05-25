using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Es.Ring.Test
{
    [TestFixture]
    public sealed class RingTf
    {
        [Test]
        public void TestThreaded()
        {
            var r = new Ring<int>(256);
            var b = new Barrier(3); // this thread, the producer thread, and the consumer thread

            long c = 0;
            long cy = 0;
            long py = 0;
            var pt = new Thread(() =>
            {
                for (var i = 0; i < 1e7; i++)
                {
                    for (;;)
                    {
                        if (r.TryAdd(i))
                        {
                            break;
                        }
                        Thread.Yield();
                        ++py;
                    }
                }
                b.RemoveParticipant();
            }) {IsBackground = true, Name = "Producer"};

            var ct = new Thread(() =>
            {
                for(;;)
                {
                    if (r.TryRead((items, i, end, mask) =>
                    {
                        for (; i < end; ++i)
                        {
                            c += items[i & mask];
                        }
                    }))
                    {
                        continue;
                    }

                    if (b.ParticipantCount < 3)
                        break;
                    Thread.Yield();
                    ++cy;
                }
                b.RemoveParticipant();
            }) {IsBackground = true, Name = "Consumer"};

            var sw = Stopwatch.StartNew();
            ct.Start();
            pt.Start();

            b.SignalAndWait();
            sw.Stop();

            Console.WriteLine($"c {c}");
            Console.WriteLine($"py {py}");
            Console.WriteLine($"cy {cy}");
            Console.WriteLine($"sw {sw.ElapsedMilliseconds}ms");
            Assert.That(!pt.IsAlive);
            Assert.That(!ct.IsAlive);

            Assert.AreEqual(49999995000000L, c);
        }
        [Test]
        public void TestThreadedBatchedProduction()
        {
            var r = new Ring<int>(256);
            var b = new Barrier(3); // this thread, the producer thread, and the consumer thread

            long c = 0;
            long cy = 0;
            long py = 0;
            var pt = new Thread(() =>
            {
                var ii = new int[100];
                var batchCount = 1e7 / ii.Length;
                for (var i = 0; i < batchCount; ++i)
                {
                    for (var j = 0; j < ii.Length; ++j)
                    {
                        ii[j] = i * ii.Length + j;
                    }
                    for (;;)
                    {
                        if (r.TryAdd(ii))
                        {
                            break;
                        }
                        Thread.Yield();
                        ++py;
                    }
                }
                b.RemoveParticipant();
            })
            { IsBackground = true, Name = "Producer" };

            var ct = new Thread(() =>
            {
                for (;;)
                {
                    if (r.TryRead((items, i, end, mask) =>
                    {
                        for (; i < end; ++i)
                        {
                            c += items[i & mask];
                        }
                    }))
                    {
                        continue;
                    }

                    if (b.ParticipantCount < 3)
                        break;
                    Thread.Yield();
                    ++cy;
                }
                b.RemoveParticipant();
            })
            { IsBackground = true, Name = "Consumer" };

            var sw = Stopwatch.StartNew();
            ct.Start();
            pt.Start();

            b.SignalAndWait();
            sw.Stop();

            Console.WriteLine($"c {c}");
            Console.WriteLine($"py {py}");
            Console.WriteLine($"cy {cy}");
            Console.WriteLine($"sw {sw.ElapsedMilliseconds}ms");
            Assert.That(!pt.IsAlive);
            Assert.That(!ct.IsAlive);

            Assert.AreEqual(49999995000000L, c);
        }
        [Test]
        public void TestTasked()
        {
            var r = new Ring<int>(256);
            var b = new Barrier(3); // this thread, the producer thread, and the consumer thread

            long c = 0;
            long cy = 0;
            long py = 0;
            Func<Task> pa = async () =>
            {
                for (var i = 0; i < 1e7; i++)
                {
                    for (;;)
                    {
                        if (r.TryAdd(i))
                        {
                            break;
                        }
                        await Task.Yield();
                        ++py;
                    }
                }
                b.RemoveParticipant();
            };

            Func<Task> ca = async () =>
            {
                for (;;)
                {
                    if (r.TryRead((items, i, end, mask) =>
                    {
                        for (; i < end; ++i)
                        {
                            c += items[i & mask];
                        }
                    }))
                    {
                        continue;
                    }

                    if (b.ParticipantCount < 3)
                        break;
                    await Task.Yield();
                    ++cy;
                }
                b.RemoveParticipant();
            };

            var sw = Stopwatch.StartNew();

            var pt = pa();
            var ct = ca();

            b.SignalAndWait();
            sw.Stop();

            Console.WriteLine($"c {c}");
            Console.WriteLine($"py {py}");
            Console.WriteLine($"cy {cy}");
            Console.WriteLine($"sw {sw.ElapsedMilliseconds}ms");
            Assert.That(pt.IsCompleted);
            Assert.That(ct.IsCompleted);

            Assert.AreEqual(49999995000000L, c);
        }

        [Test]
        public void TestTaskedBatchedProduction()
        {
            var r = new Ring<int>(256,64);
            var b = new Barrier(3); // this thread, the producer thread, and the consumer thread

            long c = 0;
            long cy = 0;
            long py = 0;

            Func<Task> pa = async () =>
            {
                var ii = new int[100];
                var batchCount = 1e7/ii.Length;
                for (var i = 0; i < batchCount; ++i)
                {
                    for (var j = 0; j < ii.Length; ++j)
                    {
                        ii[j] = i*ii.Length + j;
                    }
                    for (;;)
                    {
                        if (r.TryAdd(ii))
                        {
                            break;
                        }
                        await Task.Yield();
                        ++py;
                    }
                }
                b.RemoveParticipant();
            };

            Func<Task> ca = async () =>
            {
                for (;;)
                {
                    if (r.TryRead((items, i, end, mask) =>
                    {
                        for (; i < end; ++i)
                        {
                            c += items[i & mask];
                        }
                    }))
                    {
                        continue;
                    }

                    if (b.ParticipantCount < 3)
                        break;

                    await Task.Yield();
                    ++cy;
                }
                b.RemoveParticipant();
            };

            var sw = Stopwatch.StartNew();

            var pt = pa();
            var ct = ca();

            b.SignalAndWait();
            sw.Stop();

            Console.WriteLine($"c {c}");
            Console.WriteLine($"py {py}");
            Console.WriteLine($"cy {cy}");
            Console.WriteLine($"sw {sw.ElapsedMilliseconds}ms");
            Assert.That(pt.IsCompleted);
            Assert.That(ct.IsCompleted);

            Assert.AreEqual(49999995000000L, c);
        }
    }
}