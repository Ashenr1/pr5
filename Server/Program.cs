using System;
using System.Linq;
using Server.Classes;

namespace Server
{
    internal class Program
    {
        //127.0.0.1 //5000
        
        private static ServerConfig _config = new ServerConfig();
        private static BlacklistManager _blacklist = new BlacklistManager();
        private static LicenseServer _server = null!;

        static void Main(string[] args)
        {
            Console.Title = "ManagerLicense - Server";

            RunInitialConfiguration();

            _server = new LicenseServer(_config, _blacklist);
            _server.OnLog += (msg, color) =>
            {
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(msg);
                Console.ForegroundColor = prevColor;
            };

            _server.Start();

            Console.WriteLine();
            PrintHelp();
            Console.WriteLine();

            while (true)
            {
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                string trimmed = input.Trim();
                string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string command = parts[0].ToLower();

                switch (command)
                {
                    case "/help":
                        PrintHelp();
                        break;

                    case "/status":
                        ShowStatus();
                        break;

                    case "/disconnect":
                        if (parts.Length > 1)
                        {
                            string target = parts[1];
                            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
                            {
                                _server.DisconnectAll();
                            }
                            else
                            {
                                if (!_server.DisconnectClient(target))
                                {
                                    WriteColor($"Client with token '{target}' not found.", ConsoleColor.Red);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Usage: /disconnect <token> or /disconnect all");
                        }
                        break;

                    case "/block":
                        if (parts.Length > 1)
                        {
                            string target = parts[1];
                            // Check if target matches active client token
                            var clients = _server.GetConnectedClients();
                            var client = clients.FirstOrDefault(c => c.Token.Equals(target, StringComparison.OrdinalIgnoreCase));
                            string ipToBlock = client != null ? client.IpAddress : target;

                            _blacklist.Add(ipToBlock);

                            if (client != null)
                            {
                                _server.DisconnectClient(client.Token, "Added to blacklist");
                            }

                            WriteColor($"Target '{ipToBlock}' added to black list.", ConsoleColor.Yellow);
                        }
                        else
                        {
                            Console.WriteLine("Usage: /block <IP or Token>");
                        }
                        break;

                    case "/unblock":
                        if (parts.Length > 1)
                        {
                            string ip = parts[1];
                            if (_blacklist.Remove(ip))
                            {
                                WriteColor($"IP '{ip}' removed from black list.", ConsoleColor.Green);
                            }
                            else
                            {
                                WriteColor($"IP '{ip}' not found in black list.", ConsoleColor.Red);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Usage: /unblock <IP>");
                        }
                        break;

                    case "/blacklist":
                        ShowBlacklist();
                        break;

                    case "/config":
                        ReconfigureServer();
                        break;

                    case "/clear":
                        Console.Clear();
                        break;

                    case "/exit":
                        _server.Stop();
                        WriteColor("Server stopped. Press any key to exit...", ConsoleColor.Gray);
                        return;

                    default:
                        Console.WriteLine($"Unknown command: {command}. Type /help for available commands.");
                        break;
                }
            }
        }

        private static void RunInitialConfiguration()
        {
            Console.Write("Please provide the IP address of the license server: ");
            SetGreen();
            string? ip = Console.ReadLine();
            ResetColor();
            if (!string.IsNullOrWhiteSpace(ip) && System.Net.IPAddress.TryParse(ip.Trim(), out _))
            {
                _config.IpAddress = ip.Trim();
            }
            else
            {
                WriteColor(_config.IpAddress, ConsoleColor.Green);
            }

            Console.Write("Please specify the license server port: ");
            SetGreen();
            string? portStr = Console.ReadLine();
            ResetColor();
            if (int.TryParse(portStr?.Trim(), out int port) && port > 0 && port <= 65535)
            {
                _config.Port = port;
            }
            else
            {
                WriteColor(_config.Port.ToString(), ConsoleColor.Green);
            }

            Console.Write("Please indicate the largest number of clients: ");
            SetGreen();
            string? maxStr = Console.ReadLine();
            ResetColor();
            if (int.TryParse(maxStr?.Trim(), out int max) && max > 0)
            {
                _config.MaxClients = max;
            }
            else
            {
                WriteColor(_config.MaxClients.ToString(), ConsoleColor.Green);
            }

            Console.Write("Specify the token lifetime: ");
            SetGreen();
            string? lifeStr = Console.ReadLine();
            ResetColor();
            if (int.TryParse(lifeStr?.Trim(), out int life) && life > 0)
            {
                _config.TokenLifetime = life;
            }
            else
            {
                WriteColor(_config.TokenLifetime.ToString(), ConsoleColor.Green);
            }

            Console.Write("To change, write the command: ");
            WriteColor("/config", ConsoleColor.Green);
        }

        private static void ReconfigureServer()
        {
            WriteColor("\n--- Reconfiguration ---", ConsoleColor.Yellow);
            Console.Write($"Enter IP address [{_config.IpAddress}]: ");
            string? ip = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(ip)) _config.IpAddress = ip.Trim();

            Console.Write($"Enter Port [{_config.Port}]: ");
            string? p = Console.ReadLine();
            if (int.TryParse(p, out int port)) _config.Port = port;

            Console.Write($"Enter max clients [{_config.MaxClients}]: ");
            string? m = Console.ReadLine();
            if (int.TryParse(m, out int max)) _config.MaxClients = max;

            Console.Write($"Enter token lifetime (seconds) [{_config.TokenLifetime}]: ");
            string? l = Console.ReadLine();
            if (int.TryParse(l, out int life)) _config.TokenLifetime = life;

            WriteColor("Server restarting with new settings...", ConsoleColor.Yellow);
            _server.Stop();
            _server = new LicenseServer(_config, _blacklist);
            _server.OnLog += (msg, color) => WriteColor(msg, color);
            _server.Start();
            WriteColor("Server restarted successfully.\n", ConsoleColor.Green);
        }

        private static void ShowStatus()
        {
            var clients = _server.GetConnectedClients();
            WriteColor($"\nConnected clients: {clients.Count} / {_config.MaxClients}", ConsoleColor.Cyan);

            if (clients.Count == 0)
            {
                Console.WriteLine("No active client connections.");
                return;
            }

            foreach (var c in clients)
            {
                int duration = (int)(DateTime.Now - c.DateConnect).TotalSeconds;
                int remaining = Math.Max(0, _config.TokenLifetime - duration);
                Console.WriteLine($"Client: {c.Token}, IP: {c.IpAddress}:{c.Port}, time connection: {c.DateConnect:HH:mm:ss dd.MM}, duration: {duration}s, expires in: {remaining}s");
            }
            Console.WriteLine();
        }

        private static void ShowBlacklist()
        {
            var list = _blacklist.GetAll();
            WriteColor($"\nBlacklisted IPs ({list.Count}):", ConsoleColor.Yellow);
            if (list.Count == 0)
            {
                Console.WriteLine("Blacklist is empty.");
                return;
            }

            foreach (var ip in list)
            {
                Console.WriteLine($"- IP: {ip}");
            }
            Console.WriteLine();
        }

        private static void PrintHelp()
        {
            WriteColor("Commands to connect to the server:", ConsoleColor.Gray);
            WriteColor("/config     - set initial settings", ConsoleColor.Green);
            WriteColor("/status     - list of users connected to the server", ConsoleColor.Green);
            WriteColor("/disconnect - disconnecting users from the server (/disconnect <token> or /disconnect all)", ConsoleColor.Green);
            WriteColor("/block      - add IP or client to blacklist (/block <ip or token>) [на '4']", ConsoleColor.Green);
            WriteColor("/unblock    - remove IP from blacklist (/unblock <ip>) [на '4']", ConsoleColor.Green);
            WriteColor("/blacklist  - show blacklisted clients [на'4']", ConsoleColor.Green);
            WriteColor("/clear      - clear console window", ConsoleColor.Green);
            WriteColor("/exit       - shutdown server", ConsoleColor.Green);
        }

        private static void WriteColor(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }

        private static void SetGreen() => Console.ForegroundColor = ConsoleColor.Green;
        private static void ResetColor() => Console.ResetColor();
    }
}
