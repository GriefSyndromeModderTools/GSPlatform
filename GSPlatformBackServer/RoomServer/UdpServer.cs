using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace GSPlatformBackServer.RoomServer
{
    public interface IMessageBuffer
    {
        void Init();
        bool Check(int size);
    }

    public abstract class UdpServer<TMessage> : IHostedService
        where TMessage : unmanaged, IMessageBuffer
    {
        public delegate void DelayedTaskDelegate(int threadId, Socket socket, ref TMessage buffer);

        private readonly Thread[] _thread;
        private readonly int _port;
        private volatile bool _threadStop;
        private int _threadStartCount;
        private readonly List<DelayedTaskDelegate> _delayedTasks = new();

        protected UdpServer(int port, int threadCount)
        {
            _port = port;
            _thread = new Thread[threadCount];
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _threadStop = false;
            for (int i = 0; i < _thread.Length; ++i)
            {
                var th = new Thread(ThreadEntry);
                _thread[i] = th;
                th.Start();
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _threadStop = true;
            for (int i = 0; i < _thread.Length; ++i)
            {
                while (!cancellationToken.IsCancellationRequested && !_thread[i].Join(0))
                {
                    await Task.Delay(1, cancellationToken);
                }
            }
        }

        protected void AddDelayedTask(DelayedTaskDelegate action)
        {
            _delayedTasks.Add(action);
        }

        private void ThreadEntry()
        {
            var threadId = Interlocked.Increment(ref _threadStartCount) - 1;

            try
            {
                while (!_threadStop)
                {
                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.Any, _port));
                    socket.Blocking = false;

                    TMessage recvStruct = default;
                    recvStruct.Init();
                    Span<byte> recvBuffer = MemoryMarshal.Cast<TMessage, byte>(MemoryMarshal.CreateSpan(ref recvStruct, 1));
                    EndPoint recvEndPoint = new IPEndPoint(0, 0);

                    try
                    {
                        while (!_threadStop)
                        {
                            foreach (var action in _delayedTasks)
                            {
                                try
                                {
                                    action(threadId, socket, ref recvStruct);
                                }
                                catch
                                {
                                }
                            }

                            if (socket.Available > 0)
                            {
                                try
                                {
                                    int recvCount = socket.ReceiveFrom(recvBuffer, ref recvEndPoint);
                                    if (!recvStruct.Check(recvCount))
                                    {
                                        continue;
                                    }
                                    var recvIPEndPoint = (IPEndPoint)recvEndPoint;
                                    HandleMessage(socket, recvIPEndPoint, ref recvStruct);
                                }
                                catch
                                {
                                }
                            }
                            else
                            {
                                Thread.Sleep(1);
                            }
                        }
                    }
                    catch
                    {
                        //socket.Available may throw SocketException.
                        //Catch it to create a new socket object.
                    }
                }
            }
            catch (Exception e)
            {
                //Exception thrown when creating the socket. Nothing we can do except for logging the message.
                Console.WriteLine(e.ToString());
            }
        }

        protected abstract void HandleMessage(Socket socket, IPEndPoint src, ref TMessage msg);
    }

    internal static class SocketSendHelper
    {
        public static void SendTo<T>(this Socket socket, ref T val, EndPoint ep)
            where T : unmanaged
        {
            socket.SendTo(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref val, 1)), ep);
        }
    }
}
