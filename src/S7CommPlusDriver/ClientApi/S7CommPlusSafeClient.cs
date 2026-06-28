using System;
using System.Collections.Generic;
using System.Linq;

namespace S7CommPlusDriver.ClientApi
{
    /// <summary>
    /// Thread-safe facade for <see cref="S7CommPlusConnection"/>.
    ///
    /// S7CommPlusConnection maintains protocol state such as sequence numbers,
    /// integrity counters and a single receive queue. Calling Read/Write/Browse
    /// concurrently on the same connection can desynchronize that state. This
    /// wrapper serializes all PLC operations through one lock and should be the
    /// default entry point for UI applications, services and polling loops.
    /// </summary>
    public sealed class S7CommPlusSafeClient : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly S7CommPlusConnection _connection;
        private bool _connected;
        private bool _disposed;

        public S7CommPlusSafeClient()
            : this(new S7CommPlusConnection())
        {
        }

        public S7CommPlusSafeClient(S7CommPlusConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>
        /// Exposes the wrapped low-level connection for advanced scenarios.
        /// Do not call methods on it concurrently or outside this safe wrapper.
        /// </summary>
        public S7CommPlusConnection Connection => _connection;

        public int LastError { get; private set; }

        public bool IsConnected
        {
            get
            {
                lock (_syncRoot)
                {
                    return _connected;
                }
            }
        }

        public int Connect(string address, string password = "", string username = "", int timeoutMs = 5000)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("PLC address must not be empty.", nameof(address));
            }

            lock (_syncRoot)
            {
                EnsureNotDisposed();

                if (_connected)
                {
                    return 0;
                }

                LastError = _connection.Connect(address, password, username, timeoutMs);
                _connected = LastError == 0;
                return LastError;
            }
        }

        public void Disconnect()
        {
            lock (_syncRoot)
            {
                if (_disposed || !_connected)
                {
                    return;
                }

                try
                {
                    _connection.Disconnect();
                }
                finally
                {
                    _connected = false;
                }
            }
        }

        public int Browse(out List<VarInfo> varInfoList)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.Browse(out varInfoList);
                return LastError;
            }
        }

        public PlcTag GetPlcTagBySymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("PLC tag symbol must not be empty.", nameof(symbol));
            }

            lock (_syncRoot)
            {
                EnsureConnected();
                return _connection.getPlcTagBySymbol(symbol);
            }
        }

        public int GetListOfDatablocks(out List<S7CommPlusConnection.DatablockInfo> dbInfoList)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.GetListOfDatablocks(out dbInfoList);
                return LastError;
            }
        }

        public int GetTypeInformation(uint exploreId, out List<PObject> objList)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.GetTypeInformation(exploreId, out objList);
                return LastError;
            }
        }

        public int ReadValues(List<ItemAddress> addressList, out List<object> values, out List<ulong> errors)
        {
            if (addressList == null)
            {
                throw new ArgumentNullException(nameof(addressList));
            }

            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.ReadValues(addressList, out values, out errors);
                return LastError;
            }
        }

        public int WriteValues(List<ItemAddress> addressList, List<PValue> values, out List<ulong> errors)
        {
            if (addressList == null)
            {
                throw new ArgumentNullException(nameof(addressList));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (addressList.Count != values.Count)
            {
                throw new ArgumentException("The number of item addresses must match the number of values.", nameof(values));
            }

            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.WriteValues(addressList, values, out errors);
                return LastError;
            }
        }

        public int ReadTags(IEnumerable<PlcTag> plcTags)
        {
            if (plcTags == null)
            {
                throw new ArgumentNullException(nameof(plcTags));
            }

            var tagList = plcTags.ToList();

            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.ReadTags(tagList);
                return LastError;
            }
        }

        public int WriteTags(IEnumerable<PlcTag> plcTags)
        {
            if (plcTags == null)
            {
                throw new ArgumentNullException(nameof(plcTags));
            }

            var tagList = plcTags.ToList();

            lock (_syncRoot)
            {
                EnsureConnected();
                LastError = _connection.WriteTags(tagList);
                return LastError;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                if (_connected)
                {
                    try
                    {
                        _connection.Disconnect();
                    }
                    finally
                    {
                        _connected = false;
                    }
                }

                _disposed = true;
            }
        }

        private void EnsureConnected()
        {
            EnsureNotDisposed();

            if (!_connected)
            {
                throw new InvalidOperationException("The PLC connection is not established.");
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(S7CommPlusSafeClient));
            }
        }
    }
}
