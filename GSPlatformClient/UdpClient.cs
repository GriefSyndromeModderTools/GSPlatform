using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GSPlatformClient
{
    public interface IMessageBuffer
    {
        void Init();
        bool Check(int size);
    }

    public abstract class UdpClient<TMessage> : IDisposable
        where TMessage : unmanaged, IMessageBuffer
    {
        public delegate void DelayedTaskDelegate(Socket socket, ref TMessage buffer, byte[] bufferArray);

        private readonly Thread _thread;
        private volatile bool _threadStop;
        private readonly List<DelayedTaskDelegate> _delayedTasks = new List<DelayedTaskDelegate>();

        private Socket _socket;

        protected UdpClient()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _thread = new Thread(ThreadEntry);
        }

        public bool IsRunning => _thread?.ThreadState == ThreadState.Running;

        protected void AddDelayedTask(DelayedTaskDelegate action)
        {
            _delayedTasks.Add(action);
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Stop()
        {
            _threadStop = true;
            _thread.Join();
        }

        public IntPtr MoveSocket()
        {
            var s = Interlocked.Exchange(ref _socket, null);
            GCHandle.Alloc(s);
            return s.Handle;
        }

        public void Dispose()
        {
            _threadStop = true;
            var s = Interlocked.Exchange(ref _socket, null);
            s?.Dispose();
        }

        private static ref TMessage CreateBuffer(out byte[] array)
        {
            array = new byte[Unsafe.SizeOf<TMessage>()];
            return ref Unsafe.As<byte, TMessage>(ref array[0]);
        }

        private void ThreadEntry()
        {
            ref TMessage recvStruct = ref CreateBuffer(out var recvBuffer);
            recvStruct.Init();
            EndPoint recvEndPoint = new IPEndPoint(0, 0);
            while (!_threadStop)
            {
                var s = _socket;
                if (s == null)
                {
                    return;
                }

                foreach (var action in _delayedTasks)
                {
                    try
                    {
                        action(s, ref recvStruct, recvBuffer);
                    }
                    catch
                    {
                    }
                }
                try
                {
                    if (s.Available > 0)
                    {
                        int recvCount = s.ReceiveFrom(recvBuffer, ref recvEndPoint);
                        if (!recvStruct.Check(recvCount))
                        {
                            continue;
                        }
                        var recvIPEndPoint = (IPEndPoint)recvEndPoint;
                        HandleMessage(s, recvIPEndPoint, ref recvStruct, recvBuffer);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch
                {
                }

            }
        }

        protected abstract void HandleMessage(Socket socket, IPEndPoint src, ref TMessage msg, byte[] msgArray);
    }

    internal static class SocketSendHelper
    {
        public static void SendTo<T>(this Socket socket, ref T _, EndPoint ep, byte[] array)
            where T : unmanaged
        {
            socket.SendTo(array, Unsafe.SizeOf<T>(), SocketFlags.None, ep);
        }
    }
}
