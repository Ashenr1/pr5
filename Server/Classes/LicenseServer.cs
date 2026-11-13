using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Classes
{
    public class LicenseServer
    {
        public ServerConfig Config { get; set; }
        public BlacklistManager Blacklist { get; }
        private readonly List<Client> _clients = new();
        private readonly object _lock = new();
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public event Action<string, ConsoleColor>? OnLog;

        public LicenseServer(ServerConfig config, BlacklistManager blacklist)
        {
            Config = config;
            Blacklist = blacklist;
        }

        public void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            OnLog?.Invoke(message, color);
        }

        public void Start()
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _isRunning = true;

            if (!IPAddress.TryParse(Config.IpAddress?.Trim(), out var ip))
            {
                ip = IPAddress.Loopback;
            }
            _listener = new TcpListener(ip, Config.Port);
            _listener.Start();

            Task.Run(() => AcceptClientsAsync(_cts.Token));
            Task.Run(() => MonitorLifetimeAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();

            lock (_lock)
            {
                foreach (var client in _clients.ToList())
                {
                    try
                    {
                        client.SendMessage("DISCONNECT: Server is shutting down");
                        client.Close();
                    }
                    catch { }
                }
                _clients.Clear();
            }

            try
            {
                _listener?.Stop();
            }
            catch { }
        }

        private async Task AcceptClientsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    if (_listener == null) break;
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(ct);
                    _ = Task.Run(() => ProcessNewClient(tcpClient), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log($"[Error] Listener error: {ex.Message}", ConsoleColor.Red);
                    }
                }
            }
        }

        private void ProcessNewClient(TcpClient tcpClient)
        {
            string remoteIp = "127.0.0.1";
            int remotePort = 0;

            if (tcpClient.Client.RemoteEndPoint is IPEndPoint ep)
            {
                remoteIp = ep.Address.ToString();
                remotePort = ep.Port;
            }

            var stream = tcpClient.GetStream();
            var reader = new StreamReader(stream);
            var writer = new StreamWriter(stream) { AutoFlush = true };

            // 1. Check Blacklist (Оценка «Хорошо»)
            if (Blacklist.IsBlacklisted(remoteIp))
            {
                writer.WriteLine("ERROR: Client IP is in the blacklist");
                tcpClient.Close();
                return;
            }

            // 2. Check license limits
            lock (_lock)
            {
                if (_clients.Count >= Config.MaxClients)
                {
                    writer.WriteLine($"ERROR: No available license slots. Server limit ({Config.MaxClients}) reached.");
                    tcpClient.Close();
                    return;
                }
            }

            // 3. Register client and generate unique token
            var client = new Client
            {
                TcpClient = tcpClient,
                Stream = stream,
                Reader = reader,
                Writer = writer,
                IpAddress = remoteIp,
                Port = remotePort,
                DateConnect = DateTime.Now,
                LastPing = DateTime.Now,
                IsConnected = true
            };

            lock (_lock)
            {
                _clients.Add(client);
            }

            // Notify client of success with token and lifetime
            writer.WriteLine($"SUCCESS {client.Token} {Config.TokenLifetime}");

            // Output to server console (matches video screenshot)
            Log($"New user connected: {client.Token}", ConsoleColor.Green);

            // Handle client communication loop
            HandleClientCommunication(client);
        }

        private void HandleClientCommunication(Client client)
        {
            try
            {
                while (client.IsConnected && _isRunning)
                {
                    if (client.Reader == null) break;
                    string? line = client.Reader.ReadLine();
                    if (line == null) break;

                    string cmd = line.Trim();

                    if (cmd.StartsWith("PING"))
                    {
                        // Check if blacklisted while connected
                        if (Blacklist.IsBlacklisted(client.IpAddress))
                        {
                            client.SendMessage("DISCONNECT: Client IP was blacklisted");
                            break;
                        }

                        // Check lifetime
                        int elapsed = (int)(DateTime.Now - client.DateConnect).TotalSeconds;
                        int remaining = Config.TokenLifetime - elapsed;

                        if (remaining <= 0)
                        {
                            client.SendMessage("DISCONNECT: Token lifetime expired (renewal required)");
                            Log($"Client {client.Token} token lifetime expired", ConsoleColor.Yellow);
                            break;
                        }

                        client.LastPing = DateTime.Now;
                        client.SendMessage($"PONG {remaining}");
                    }
                    else if (cmd.StartsWith("RENEW"))
                    {
                        var oldToken = client.Token;
                        client.GenerateNewToken();
                        client.SendMessage($"RENEWED {client.Token} {Config.TokenLifetime}");
                        Log($"Client {oldToken} renewed token: new token {client.Token}", ConsoleColor.Cyan);
                    }
                    else if (cmd.Equals("STATUS", StringComparison.OrdinalIgnoreCase))
                    {
                        int duration = (int)(DateTime.Now - client.DateConnect).TotalSeconds;
                        int remaining = Math.Max(0, Config.TokenLifetime - duration);
                        client.SendMessage($"STATUS_INFO {client.Token} {client.DateConnect:HH:mm:ss dd.MM} {duration} {remaining} {client.IpAddress} {client.Port}");
                    }
                    else if (cmd.Equals("DISCONNECT", StringComparison.OrdinalIgnoreCase))
                    {
                        client.SendMessage("BYE");
                        break;
                    }
                }
            }
            catch
            {
                // Connection dropped
            }
            finally
            {
                RemoveClient(client, "Connection closed");
            }
        }

        private void MonitorLifetimeAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    Thread.Sleep(1000);
                    List<Client> expiredClients = new();

                    lock (_lock)
                    {
                        foreach (var client in _clients)
                        {
                            int elapsed = (int)(DateTime.Now - client.DateConnect).TotalSeconds;
                            if (elapsed >= Config.TokenLifetime)
                            {
                                expiredClients.Add(client);
                            }
                        }
                    }

                    foreach (var client in expiredClients)
                    {
                        Log($"Client {client.Token} disconnected (token lifetime expired)", ConsoleColor.Yellow);
                        client.SendMessage("DISCONNECT: Token lifetime expired (renewal required)");
                        RemoveClient(client, "Lifetime expired");
                    }
                }
                catch { }
            }
        }

        public bool DisconnectClient(string token, string reason = "Disconnected by server administrator")
        {
            Client? target = null;
            lock (_lock)
            {
                target = _clients.FirstOrDefault(c => c.Token.Equals(token, StringComparison.OrdinalIgnoreCase));
            }

            if (target != null)
            {
                target.SendMessage($"DISCONNECT: {reason}");
                RemoveClient(target, reason);
                Log($"Client {target.Token} disconnected from server", ConsoleColor.Cyan);
                return true;
            }

            return false;
        }

        public void DisconnectAll(string reason = "Disconnected by server administrator")
        {
            List<Client> copy;
            lock (_lock)
            {
                copy = _clients.ToList();
            }

            foreach (var client in copy)
            {
                client.SendMessage($"DISCONNECT: {reason}");
                RemoveClient(client, reason);
            }
            Log("All clients disconnected from server", ConsoleColor.Cyan);
        }

        private void RemoveClient(Client client, string reason)
        {
            bool removed = false;
            lock (_lock)
            {
                removed = _clients.Remove(client);
            }

            if (removed)
            {
                client.Close();
            }
        }

        public List<Client> GetConnectedClients()
        {
            lock (_lock)
            {
                return _clients.ToList();
            }
        }

        public int ConnectedCount
        {
            get
            {
                lock (_lock) return _clients.Count;
            }
        }
    }
}
