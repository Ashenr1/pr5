using System;
using System.Collections.Generic;
using System.IO;

namespace Server.Classes
{
    public class BlacklistManager
    {
        private readonly HashSet<string> _blacklistedIps = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _filePath;

        public BlacklistManager(string filePath = "blacklist.txt")
        {
            _filePath = filePath;
            Load();
        }

        public bool IsBlacklisted(string ip)
        {
            lock (_blacklistedIps)
            {
                return _blacklistedIps.Contains(ip);
            }
        }

        public bool Add(string ip)
        {
            lock (_blacklistedIps)
            {
                if (_blacklistedIps.Add(ip))
                {
                    Save();
                    return true;
                }
                return false;
            }
        }

        public bool Remove(string ip)
        {
            lock (_blacklistedIps)
            {
                if (_blacklistedIps.Remove(ip))
                {
                    Save();
                    return true;
                }
                return false;
            }
        }

        public List<string> GetAll()
        {
            lock (_blacklistedIps)
            {
                return new List<string>(_blacklistedIps);
            }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var lines = File.ReadAllLines(_filePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            _blacklistedIps.Add(trimmed);
                        }
                    }
                }
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                File.WriteAllLines(_filePath, _blacklistedIps);
            }
            catch { }
        }
    }
}

