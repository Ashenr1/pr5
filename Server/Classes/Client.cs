using System;
using System.IO;
using System.Net.Sockets;

namespace Server.Classes
{
    public class Client
    {
        public string Token { get; set; }
        public DateTime DateConnect { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public DateTime LastPing { get; set; }
        public bool IsConnected { get; set; }

        public TcpClient? TcpClient { get; set; }
        public NetworkStream? Stream { get; set; }
        public StreamReader? Reader { get; set; }
        public StreamWriter? Writer { get; set; }

        public Client()
        {
            Random random = new Random();
            string Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] tokenChars = new char[10];
            for (int i = 0; i < 10; i++)
            {
                tokenChars[i] = Chars[random.Next(Chars.Length)];
            }
            Token = new string(tokenChars);
            DateConnect = DateTime.Now;
            LastPing = DateTime.Now;
            IsConnected = true;
            IpAddress = "127.0.0.1";
            Port = 0;
        }

        public void GenerateNewToken()
        {
            Random random = new Random();
            string Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            char[] tokenChars = new char[10];
            for (int i = 0; i < 10; i++)
            {
                tokenChars[i] = Chars[random.Next(Chars.Length)];
            }
            Token = new string(tokenChars);
            DateConnect = DateTime.Now;
            LastPing = DateTime.Now;
        }

        public void SendMessage(string message)
        {
            try
            {
                if (Writer != null && IsConnected)
                {
                    Writer.WriteLine(message);
                    Writer.Flush();
                }
            }
            catch
            {
                IsConnected = false;
            }
        }

        public void Close()
        {
            IsConnected = false;
            try { Reader?.Dispose(); } catch { }
            try { Writer?.Dispose(); } catch { }
            try { Stream?.Dispose(); } catch { }
            try { TcpClient?.Close(); } catch { }
        }
    }
}
