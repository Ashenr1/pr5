using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Classes
{
    public class LicenseClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _heartbeatCts;
        private readonly object _lock = new();

        public string Token { get; private set; } = string.Empty;
        public DateTime DateConnect { get; private set; }
        public int Duration => IsConnected ? (int)(DateTime.Now - DateConnect).TotalSeconds : 0;
        public string LocalIp { get; private set; } = "127.0.0.1";
        public int LocalPort { get; private set; } = 0;
        public int TokenLifetime { get; private set; } = 120;
        public bool IsConnected { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public event Action<string>? OnDisconnected;
        public event Action<string>? OnTokenRenewed;

        public bool Connect(string ip, int port)
        {
            try
            {
                DisconnectInternal(notify: false, reason: "Reconnecting");

                _tcpClient = new TcpClient();
                _tcpClient.Connect(ip, port);

                if (_tcpClient.Client.LocalEndPoint is IPEndPoint ep)
                {
                    LocalIp = ep.Address.ToString();
                    LocalPort = ep.Port;
                }

                _stream = _tcpClient.GetStream();
                _reader = new StreamReader(_stream);
                _writer = new StreamWriter(_stream) { AutoFlush = true };

                // Read server welcome response
                string? response = _reader.ReadLine();
                if (response == null)
                {
                    LastError = "Server closed connection immediately.";
                    CloseConnection();
                    return false;
                }

                if (response.StartsWith("SUCCESS"))
                {
                    var parts = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    Token = parts.Length > 1 ? parts[1] : string.Empty;
                    if (parts.Length > 2 && int.TryParse(parts[2], out int life))
                    {
                        TokenLifetime = life;
                    }

                    DateConnect = DateTime.Now;
                    IsConnected = true;
                    LastError = string.Empty;

                    StartHeartbeat();
                    return true;
                }
                else if (response.StartsWith("ERROR:"))
                {
                    LastError = response.Substring(6).Trim();
                    CloseConnection();
                    return false;
                }
                else
                {
                    LastError = response;
                    CloseConnection();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                CloseConnection();
                return false;
            }
        }

        public bool RenewToken()
        {
            if (!IsConnected || _writer == null || _reader == null)
            {
                LastError = "Not connected to server";
                return false;
            }

            lock (_lock)
            {
                try
                {
                    _writer.WriteLine("RENEW");
                    string? reply = _reader.ReadLine();
                    if (reply != null && reply.StartsWith("RENEWED"))
                    {
                        var parts = reply.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            Token = parts[1];
                            DateConnect = DateTime.Now;
                            OnTokenRenewed?.Invoke(Token);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }
            }

            return false;
        }

        public void Disconnect()
        {
            DisconnectInternal(notify: true, reason: "Disconnected by user");
        }

        private void DisconnectInternal(bool notify, string reason)
        {
            if (!IsConnected) return;

            try
            {
                if (_writer != null)
                {
                    _writer.WriteLine("DISCONNECT");
                }
            }
            catch { }

            CloseConnection();

            if (notify)
            {
                OnDisconnected?.Invoke(reason);
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        await Task.Delay(3000, token);
                        if (!IsConnected) break;

                        lock (_lock)
                        {
                            if (_writer != null)
                            {
                                _writer.WriteLine("PING");
                            }
                        }

                        string? reply = null;
                        if (_reader != null)
                        {
                            reply = await _reader.ReadLineAsync();
                        }

                        if (reply == null)
                        {
                            TriggerDisconnect("Server closed connection");
                            break;
                        }

                        if (reply.StartsWith("DISCONNECT"))
                        {
                            string reason = reply.Contains(":") ? reply.Substring(reply.IndexOf(':') + 1).Trim() : reply;
                            TriggerDisconnect(reason);
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        if (IsConnected)
                        {
                            TriggerDisconnect("Connection lost with server");
                        }
                        break;
                    }
                }
            }, token);
        }

        private void TriggerDisconnect(string reason)
        {
            if (!IsConnected) return;
            IsConnected = false;
            CloseConnection();
            OnDisconnected?.Invoke(reason);
        }

        private void CloseConnection()
        {
            IsConnected = false;
            try { _heartbeatCts?.Cancel(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }
            _tcpClient = null;
            _stream = null;
            _reader = null;
            _writer = null;
        }
    }
}
