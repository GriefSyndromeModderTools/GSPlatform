using System.Collections.Concurrent;

namespace GSPlatformBackServer.Helpers
{
    internal sealed class ConcurrentRecentList<T>
        where T : notnull
    {
        private readonly ConcurrentDictionary<T, (DateTime Time, ulong Seq)> _dict = new();
        private readonly SemaphoreSlim _sortLock = new(1);
        private UpdateTime _lastUpdateTime;
        private ulong _updateSeqence = 0;

        private readonly TimeSpan _checkInterval, _maxLifetime;

        public ConcurrentRecentList(DateTime now, TimeSpan checkInterval, TimeSpan maxLifetime)
        {
            _checkInterval = checkInterval;
            _maxLifetime = maxLifetime;
            _lastUpdateTime = new() { Time = now };
        }

        private sealed class UpdateTime
        {
            public DateTime Time;
        }

        public IEnumerable<T> Elements => _dict.Keys;
        public int Count => _dict.Count;

        public bool Add(T key, DateTime now)
        {
            //_dict[key] = now;
            var updateSequence = Interlocked.Increment(ref _updateSeqence);
            var ret = _dict.AddOrUpdate(key,
                static (k, tuple) => tuple,
                static (k, v, tuple) => (tuple.Time, v.Seq),
                (Time: now, Seq: updateSequence)).Seq == updateSequence;
            if (_lastUpdateTime.Time + _checkInterval < now)
            {
                if (_sortLock.Wait(0))
                {
                    try
                    {
                        _lastUpdateTime = new() { Time = now };
                        DoSort(now);
                    }
                    finally
                    {
                        _sortLock.Release();
                    }
                }
            }
            return ret;
        }

        public void DoSort(DateTime excludeTime)
        {
            foreach (var (k, v) in _dict)
            {
                if (v.Time < excludeTime)
                {
                    if (_dict.TryRemove(k, out var newV))
                    {
                        if (newV.Time >= excludeTime)
                        {
                            _dict.AddOrUpdate(k,
                                static (k, newV) => newV,
                                static (k, currentV, newV) => currentV.Time > newV.Time ? currentV : newV,
                                newV);
                        }
                    }
                }
            }
        }

        public bool Contains(T key, DateTime timeLimit)
        {
            if (!_dict.TryGetValue(key, out var v))
            {
                return false;
            }
            return v.Time >= timeLimit;
        }
    }
}
