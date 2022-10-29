using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GSPlatformRelayServer.RelayServer
{
    public interface IMessageBuffer
    {
        void Init();
        bool Check(int size);
    }

    //This class is slightly different from the one in main server:
    //Recv and send methods contain explicit length, to allow arbitrary message to be forwarded.
    //Sockets are blocking, and delayed tasks are executed after each RecvFrom returns, which reduces latency.
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

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, _port));
            socket.Blocking = true;

            TMessage recvStruct = default;
            TMessage sendStruct = default;
            recvStruct.Init();
            sendStruct.Init();
            Span<byte> recvBuffer = MemoryMarshal.Cast<TMessage, byte>(MemoryMarshal.CreateSpan(ref recvStruct, 1));
            Span<byte> sendBuffer = MemoryMarshal.Cast<TMessage, byte>(MemoryMarshal.CreateSpan(ref sendStruct, 1));
            EndPoint recvEndPoint = new IPEndPoint(0, 0);
            while (!_threadStop)
            {
                try
                {
                    int recvCount = socket.ReceiveFrom(recvBuffer, ref recvEndPoint);

                    if (!recvStruct.Check(recvCount))
                    {
                        continue;
                    }
                    var recvIPEndPoint = (IPEndPoint)recvEndPoint;
                    HandleMessage(socket, recvIPEndPoint, ref recvStruct, recvCount);

                    foreach (var action in _delayedTasks)
                    {
                        try
                        {
                            action(threadId, socket, ref sendStruct); //Don't touch recv buffer.
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
        }

        protected abstract void HandleMessage(Socket socket, IPEndPoint src, ref TMessage msg, int recvLength);
    }

    internal static class SocketSendHelper
    {
        public static void SendTo<T>(this Socket socket, ref T val, int? size, EndPoint ep)
            where T : unmanaged
        {
            socket.SendTo(MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref val), size ?? Unsafe.SizeOf<T>()), ep);
        }
    }
}
