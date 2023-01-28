using System.Text;

namespace FFmpegGUI_Server
{
    internal class KeyLoader
    {
        private string _path;
        private List<KeyStatus> _keys = new();

        public DateTime AfterYear => DateTime.Now.AddYears(1);
        public DateTime AfterMonth => DateTime.Now.AddMonths(1);
        public DateTime AfterWeek => DateTime.Now.AddDays(7);

        public KeyLoader(string keyPath = "Keys/db.json")
        {
            _path = keyPath;

            if(!Directory.Exists(Path.GetDirectoryName(_path)))
                Directory.CreateDirectory(Path.GetDirectoryName(_path));

            if (!File.Exists(_path))
                File.Create(_path).Close();

            LoadKeys();

            Thread thread = new Thread((e) => { FilterKeys(); });
        }

        private void FilterKeys()
        {
            while (true)
            {
                Thread.Sleep(1000 * 60 * 60); // hour delay

                lock (_keys)
                {
                    List<KeyStatus> finalKeys = new();

                    _keys.ForEach((key) =>
                    {
                        if (DateTime.Now < key.expirationTime)
                            finalKeys.Add(key);
                    });

                    _keys = finalKeys;
                }
            }
        }

        public bool TryRemoveKey(string key)
        {
            KeyStatus status = _keys.Find((keyStatus) => keyStatus.key == key);

            if (status != null)
            {
                _keys.Remove(status);
                SaveKeys();
                return true;
            }

            return false;
        }

        public string TryAddKey(string key, DateTime expirationTime)
        {
            bool contains = ContainsKey(key);
            if (!contains)
                _keys.Add(new KeyStatus(key, expirationTime));
            SaveKeys();

            return contains ? "" : key;
        }

        public string AddKey(string key, DateTime expirationTime)
        {
            _keys.Add(new KeyStatus(key, expirationTime));
            return key;
        }

        public string LinkedID(string key)
        {
            return _keys.Find((keyStatus) => keyStatus.key == key).userID;
        }

        public void LinkKey(string key, string userID)
        {
            _keys.Find((keyStatus) => keyStatus.key == key).userID = userID;
            SaveKeys();
        }

        public string GenerateKey(DateTime expirationTime)
        {
            string key = GenerateKey();

            while (ContainsKey(key))
                key = GenerateKey();

            AddKey(key.ToString(), expirationTime);
            SaveKeys();

            return key;
        }

        private string GenerateKey()
        {
            StringBuilder key = new StringBuilder();

            for (int i = 0; i < 12; i++)
            {
                Random random = new Random();
                random.Next(0, 9);

                key.Append(random.Next(9));
            }

            return key.ToString();
        }

        public bool ContainsKey(string key)
        {
            bool ok = false;

            _keys.ForEach((keyStatus) =>
            {
                if (keyStatus.key == key)
                    ok = true;
            });

            return ok;
        }

        public void Clear()
        {
            _keys.Clear();
            SaveKeys();
        }

        private void SaveKeys()
        {
            File.WriteAllText(_path, Newtonsoft.Json.JsonConvert.SerializeObject(_keys));
        }

        private void LoadKeys()
        {
            _keys = Newtonsoft.Json.JsonConvert.DeserializeObject<List<KeyStatus>>(File.ReadAllText(_path));
            if (_keys == null)
                _keys = new();
        }
    }

    [Serializable]
    internal class KeyStatus
    {
        public string key = "";
        public string userID = "";
        public DateTime expirationTime;

        public KeyStatus(string key, DateTime expiration)
        {
            this.key = key;
            this.expirationTime = expiration;
        }
    }
}
