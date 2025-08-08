using System.Diagnostics.Tracing;
using System.Collections.Concurrent;

namespace UmbralSocket.Net.Diagnostics
{
    /// <summary>
    /// EventSource for UmbralSocket observability.
    /// </summary>
    [EventSource(Name = "UmbralSocket")]
    internal sealed class UmbralSocketEventSource : EventSource
    {
        public static readonly UmbralSocketEventSource Log = new UmbralSocketEventSource();

        private PollingCounter? _connectionsActive;
        private IncrementingPollingCounter? _bytesSent;
        private IncrementingPollingCounter? _bytesReceived;
        private PollingCounter? _sendQueueLength;
        private PollingCounter? _receiveQueueLength;
        private PollingCounter? _latencyMeanMs;
        private PollingCounter? _latencyP50Ms;
        private PollingCounter? _latencyP95Ms;
        private PollingCounter? _latencyP99Ms;

        private int _connections;
        private long _sent;
        private long _received;
        private int _sendQueue;
        private int _receiveQueue;
        private double _latencyMean;
        private double _latencyP50;
        private double _latencyP95;
        private double _latencyP99;

        // Lightweight latency tracking
        private readonly ConcurrentQueue<double> _latencyWindow = new();
        private const int MaxLatencyWindow = 1000;

        private UmbralSocketEventSource()
        {
            _connectionsActive = new PollingCounter("connections-active", this, () => _connections);
            _bytesSent = new IncrementingPollingCounter("bytes-sent", this, () => _sent);
            _bytesReceived = new IncrementingPollingCounter("bytes-received", this, () => _received);
            _sendQueueLength = new PollingCounter("send-queue-length", this, () => _sendQueue);
            _receiveQueueLength = new PollingCounter("receive-queue-length", this, () => _receiveQueue);
            _latencyMeanMs = new PollingCounter("latency-mean-ms", this, () => _latencyMean);
            _latencyP50Ms = new PollingCounter("latency-p50-ms", this, () => _latencyP50);
            _latencyP95Ms = new PollingCounter("latency-p95-ms", this, () => _latencyP95);
            _latencyP99Ms = new PollingCounter("latency-p99-ms", this, () => _latencyP99);
        }

        // Methods to update counters (to be called from transport code)
        public void ConnectionChanged(int active) => _connections = active;
        public void BytesSent(long value) => _sent += value;
        public void BytesReceived(long value) => _received += value;
        public void SendQueueLength(int value) => _sendQueue = value;
        public void ReceiveQueueLength(int value) => _receiveQueue = value;
        
        public void RecordLatency(double latencyMs)
        {
            _latencyWindow.Enqueue(latencyMs);
            
            // Keep window size manageable
            while (_latencyWindow.Count > MaxLatencyWindow)
            {
                _latencyWindow.TryDequeue(out _);
            }
            
            // Update percentiles periodically (every 100 samples)
            if (_latencyWindow.Count % 100 == 0)
            {
                UpdateLatencyPercentiles();
            }
        }

        private void UpdateLatencyPercentiles()
        {
            var samples = _latencyWindow.ToArray();
            if (samples.Length == 0) return;

            Array.Sort(samples);
            
            _latencyMean = samples.Sum() / samples.Length;
            _latencyP50 = GetPercentile(samples, 0.5);
            _latencyP95 = GetPercentile(samples, 0.95);
            _latencyP99 = GetPercentile(samples, 0.99);
        }

        private static double GetPercentile(double[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 0) return 0;
            if (sortedValues.Length == 1) return sortedValues[0];
            
            var index = percentile * (sortedValues.Length - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);
            
            if (lower == upper) return sortedValues[lower];
            
            var weight = index - lower;
            return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        }

        public void LatencyStats(double mean, double p50, double p95, double p99)
        {
            _latencyMean = mean;
            _latencyP50 = p50;
            _latencyP95 = p95;
            _latencyP99 = p99;
        }
    }
}
