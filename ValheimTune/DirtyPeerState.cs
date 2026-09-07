using System;
using System.Collections.Generic;

namespace ValheimTune
{
    // Per-peer bookkeeping for B1. No game types: TId is ZDOID in production, int in tests.
    public sealed class DirtyPeerState<TId>
    {
        private (int x, int y) _lastZone = (int.MinValue, int.MinValue);
        private float _lastFullScan = float.NegativeInfinity;
        private bool _wasActive;
        private readonly List<TId> _prune = new List<TId>();

        public HashSet<TId> Pending { get; } = new HashSet<TId>();
        public int FullScans { get; private set; }

        // active reflects whether DirtySets was actually in effect (not disabled by the watchdog)
        // on this call. Coming back from an inactive round always forces a full scan, since Pending
        // was not being fed while inactive and may be stale.
        public bool NeedsFullScan((int x, int y) zone, float now, float reconcileSeconds, bool active)
        {
            bool full = !_wasActive || zone != _lastZone || now - _lastFullScan > reconcileSeconds;
            _wasActive = active;
            if (full)
            {
                _lastZone = zone;
                _lastFullScan = now;
                FullScans++;
            }
            return full;
        }

        public void Drain(List<TId> into, Func<TId, bool> exists, Func<TId, bool> inArea, Func<TId, bool> shouldSend, Func<TId, bool> deferSend = null)
        {
            _prune.Clear();
            foreach (var id in Pending)
            {
                if (!exists(id) || !inArea(id) || !shouldSend(id)) { _prune.Add(id); continue; }
                if (deferSend != null && deferSend(id)) continue;    // skip this round, stays pending
                into.Add(id);
            }
            foreach (var id in _prune) Pending.Remove(id);
        }
    }
}
