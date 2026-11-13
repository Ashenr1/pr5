using System;
using Client.Classes;

namespace Client
{
    internal class Program
    {
        private static ClientConfig _config = new ClientConfig();
        private static LicenseClient _client = new LicenseClient();

        static void Main(string[] args)
        {
            Console.Title = "ManagerLicense - Client";

            _client.OnDisconnected += reason =>
            {
                WriteColor("\nThe client is disconnected from the server", ConsoleColor.Red);
                Console.Write("> ");
            };

            _client.OnTokenRenewed += newToken =>
            {
                WriteColor($"\nToken successfully renewed! New token: {newToken}", ConsoleColor.Cyan);
                Console.Write("> ");
            };

            RunInitialConfiguration();

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

                    case "/config":
                        ReconfigureClient();
                        break;

                    case "/connect":
                        HandleConnect();
                        break;

                    case "/status":
                        ShowStatus();
                        break;

                    case "/renew":
                        HandleRenew();
                        break;

                    case "/disconnect":
                        if (_client.IsConnected)
                        {
                            _client.Disconnect();
                            WriteColor("Disconnected from server.", ConsoleColor.Cyan);
                        }
                        else
                        {
                            WriteColor("Client is not currently connected.", ConsoleColor.Yellow);
                        }
                        break;

                    case "/clear":
                        Console.Clear();
                        break;

                    case "/exit":
                        if (_client.IsConnected) _client.Disconnect();
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
                _config.ServerIp = ip.Trim();
            }
            else
            {
                WriteColor(_config.ServerIp, ConsoleColor.Green);
            }

            Console.Write("Please specify the license server port: ");
            SetGreen();
            string? portStr = Console.ReadLine();
            ResetColor();
            if (int.TryParse(portStr?.Trim(), out int port) && port > 0 && port <= 65535)
            {
                _config.ServerPort = port;
            }
            else
            {
                WriteColor(_config.ServerPort.ToString(), ConsoleColor.Green);
            }

            Console.Write("To change, write the command: ");
            WriteColor("/config", ConsoleColor.Green);
        }

        private static void ReconfigureClient()
        {
            WriteColor("\n--- Reconfiguration ---", ConsoleColor.Yellow);
            Console.Write($"Enter Server IP [{_config.ServerIp}]: ");
            string? ip = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(ip)) _config.ServerIp = ip.Trim();

            Console.Write($"Enter Server Port [{_config.ServerPort}]: ");
            string? p = Console.ReadLine();
            if (int.TryParse(p, out int port)) _config.ServerPort = port;

            WriteColor("Configuration updated.\n", ConsoleColor.Green);
        }

        private static void HandleConnect()
        {
            if (_client.IsConnected)
            {
                WriteColor("Already connected to server. Use /status or /disconnect.", ConsoleColor.Yellow);
                return;
            }

            bool ok = _client.Connect(_config.ServerIp, _config.ServerPort);

            if (ok)
            {
                WriteColor("Connection to server successful", ConsoleColor.Green);
                WriteColor($"Received connection token: {_client.Token}", ConsoleColor.Green);
            }
            else
            {
                WriteColor($"Connection failed: {_client.LastError}", ConsoleColor.Red);
            }
        }

        private static void ShowStatus()
        {
            if (!_client.IsConnected)
            {
                WriteColor("The client is not connected to any server. Type /connect to connect.", ConsoleColor.Red);
                return;
            }

            // Exact match with teacher's demo:
            // Client: swZXg0vr0i, time connection: 18:45:28 12.10, duration: 10
            Console.WriteLine($"Client: {_client.Token}, time connection: {_client.DateConnect:HH:mm:ss dd.MM}, duration: {_client.Duration}");
        }

        private static void HandleRenew()
        {
            if (!_client.IsConnected)
            {
                WriteColor("Cannot renew token: client is not connected.", ConsoleColor.Red);
                return;
            }

            if (_client.RenewToken())
            {
                WriteColor($"Token successfully renewed: {_client.Token}", ConsoleColor.Green);
            }
            else
            {
                WriteColor($"Failed to renew token: {_client.LastError}", ConsoleColor.Red);
            }
        }

        private static void PrintHelp()
        {
            WriteColor("Commands to connect to the server:", ConsoleColor.Gray);
            WriteColor("/config     - set initial settings", ConsoleColor.Green);
            WriteColor("/connect    - connection to the server", ConsoleColor.Green);
            WriteColor("/status     - connection to the server", ConsoleColor.Green);
            WriteColor("/renew      - renew current license token", ConsoleColor.Green);
            WriteColor("/disconnect - disconnect from the server", ConsoleColor.Green);
            WriteColor("/clear      - clear console window", ConsoleColor.Green);
            WriteColor("/exit       - exit application", ConsoleColor.Green);
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
