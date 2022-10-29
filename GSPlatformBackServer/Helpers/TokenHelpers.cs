namespace GSPlatformBackServer.Helpers
{
    internal static class TokenHelpers
    {
        public static string CreateUserToken()
        {
            return "U" +
                BitConverter.DoubleToUInt64Bits(DateTime.UtcNow.ToOADate()).ToString("X16") +
                Transform((ulong)Random.Shared.NextInt64()).ToString("X16");
        }

        private static ulong _roomToken = 0;
        private static readonly ulong[] _roomTokenTransform = new ulong[]
        {
            (ulong)Random.Shared.NextInt64(),
            RemoveZeros((ulong)Random.Shared.NextInt64()),
            (ulong)Random.Shared.NextInt64(),
            RemoveZeros((ulong)Random.Shared.NextInt64()),
        };

        private static ulong RemoveZeros(ulong x)
        {
            while ((x & 1) != 0)
            {
                x >>= 1;
            }
            return x;
        }

        private static ulong Transform(ulong t)
        {
            t = (t + _roomTokenTransform[0]) * _roomTokenTransform[1];
            t = (t + _roomTokenTransform[1]) * _roomTokenTransform[2];
            return t;
        }

        public static ulong CreateRoomId()
        {
            var t = Interlocked.Increment(ref _roomToken);
            return Transform(t);
        }

        public static ulong CreateRoomUserToken()
        {
            return CreateRoomId();
        }
    }
}
