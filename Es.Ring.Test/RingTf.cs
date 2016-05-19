using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;

namespace Es.Ring.Test
{
    [TestFixture]
    public sealed class RingTf
    {
        [Test]
        public void Test()
        {
            var r = new Ring<int>(256);
            var b = new Barrier(3); // this thread, the producer thread, and the consumer thread

            long c = 0;
            long cy = 0;
            long py = 0;
            var pt = new Thread(() =>
            {
                for (var i = 0; i < 1e6; i++)
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
                while (b.ParticipantCount > 2)
                {
                    if (!r.TryRead((items, i, end, mask) =>
                    {
                        for (; i < end; ++i)
                        {
                            c += items[i & mask];
                        }
                    }))
                    {
                        Thread.Yield();
                        ++cy;
                    }
                    ;
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

            Assert.AreEqual(499999500000, c);
        }
    }
}