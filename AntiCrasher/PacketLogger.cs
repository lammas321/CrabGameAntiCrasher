using BepInEx;
using SteamworksNative;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace AntiCrasher
{
    internal static class PacketLogger
    {
        internal sealed class PacketLog
        {
            internal DateTime time;
            internal ulong senderId;
            internal string senderName;
            internal int channel;
            internal int packetThisFrame;
            internal byte[] bytes;
        }


        private static readonly ConcurrentQueue<PacketLog> packetQueue = new();
        private static readonly AutoResetEvent signal = new(false);

        private static Thread writerThread;
        private static volatile bool writerRunning;


        internal static void Init()
        {
            StartWriterThread();
        }

        private static void StartWriterThread()
        {
            if (writerRunning)
                return;

            writerRunning = true;

            writerThread = new Thread(PacketWriterLoop)
            {
                IsBackground = true,
                Name = "AntiCrasherPacketWriter"
            };

            writerThread.Start();

            AntiCrasher.Instance.Log.LogInfo("Started PacketLogger writer thread");
        }

        private static void PacketWriterLoop()
        {
            string path = Path.Combine(Paths.BepInExRootPath, "PacketLog.txt");

            try
            {
                using FileStream fs = new(
                    path,
                    FileMode.Truncate,
                    FileAccess.Write,
                    FileShare.Read,
                    8192,
                    FileOptions.WriteThrough
                );

                using StreamWriter writer = new(fs, Encoding.UTF8)
                {
                    AutoFlush = false
                };

                StringBuilder batchBuilder = new(16384);

                while (writerRunning)
                {
                    signal.WaitOne(100);

                    bool wroteAnything = false;

                    while (packetQueue.TryDequeue(out PacketLog log))
                    {
                        wroteAnything = true;

                        batchBuilder.Append($"{DateTime.Now,-11:h:mm:ss tt} ")
                            .Append(log.senderName)
                            .Append(" (@")
                            .Append(log.senderId)
                            .Append(") ");

                        batchBuilder.Append('#')
                            .Append(log.packetThisFrame)
                            .Append(' ')
                            .Append((SteamPacketManager_NetworkChannel)log.channel)
                            .Append(" Len:")
                            .Append(log.bytes.Length)
                            .Append(' ');

                        batchBuilder.Append(BitConverter.ToString(log.bytes));
                        batchBuilder.AppendLine();

                        // Prevent massive string growth
                        if (batchBuilder.Length > 65536)
                        {
                            writer.Write(batchBuilder.ToString());
                            batchBuilder.Clear();
                            writer.Flush();
                        }
                    }

                    if (wroteAnything && batchBuilder.Length > 0)
                    {
                        writer.Write(batchBuilder.ToString());
                        batchBuilder.Clear();
                        writer.Flush();
                    }
                }

                // Final flush on shutdown
                if (batchBuilder.Length > 0)
                {
                    writer.Write(batchBuilder.ToString());
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                writerRunning = false;
                try
                {
                    File.AppendAllText(path,
                        "\n[PacketWriterLoop crashed]\n" + e + "\n");
                }
                catch { }
            }
        }


        internal static void EnqueuePacket(ulong senderId, int channel, int packetThisFrame, byte[] data)
        {
            if (!AntiCrasher.Instance.packetLogging || !writerRunning || senderId == 0ul)
                return;
            
            packetQueue.Enqueue(new PacketLog
            {
                time = DateTime.Now,
                senderId = senderId,
                senderName = SteamFriends.GetFriendPersonaName(new CSteamID(senderId)),
                channel = channel,
                packetThisFrame = packetThisFrame,
                bytes = data
            });
            signal.Set();
        }
    }
}