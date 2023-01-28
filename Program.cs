using FFmpegGUI_Server;
using Riptide;
using Riptide.Utils;

internal class Program
{
    private static Server _server;
    private static KeyLoader _keys;
    private static bool _running;

    private static void Main()
    {
        Console.Title = "FFmpegGUI-Server";
        Console.WriteLine("Write EXIT to stop the program.");

        RiptideLogger.Initialize(Console.WriteLine, true);
        _running = true;

        _keys = new KeyLoader();
        new Thread(new ThreadStart(Loop)).Start();

        while (_running)
        {
            string command = Console.ReadLine();
            string[] commandParts = command.Split(' ');

            if (command.ToLower() == "exit")
                _running = false;
            else if (commandParts.Length > 1 && commandParts[0] == "generate")
            {
                string key = "";

                if (commandParts[1] == "y")
                    key = _keys.GenerateKey(_keys.AfterYear);
                else if (commandParts[1] == "m")
                    key = _keys.GenerateKey(_keys.AfterMonth);
                else if (commandParts[1] == "w")
                    key = _keys.GenerateKey(_keys.AfterWeek);

                if (key != "")
                    Console.WriteLine("Generated key is: " + key);
            }
            else if (commandParts.Length > 2 && commandParts[0] == "add")
            {
                string key = "";

                if (commandParts[2] == "y")
                    key = _keys.TryAddKey(commandParts[1], _keys.AfterYear);
                else if (commandParts[2] == "m")
                    key = _keys.TryAddKey(commandParts[1], _keys.AfterMonth);
                else if (commandParts[2] == "w")
                    key = _keys.TryAddKey(commandParts[1], _keys.AfterWeek);

                if (key != "")
                    Console.WriteLine("Added key is: " + key);
            }
            else if (commandParts.Length > 1 && commandParts[0] == "remove")
            {
                if (_keys.TryRemoveKey(commandParts[1]))
                    Console.WriteLine("Deleted key is: " + commandParts[1]);
            }
            else if (command.ToLower() == "clear")
            {
                _keys.Clear();
            }
            else
                Console.WriteLine("Unknown command. Known commands:\n" +
                    "EXIT - exit application\n" +
                    "generate [y/m/w] - generate key for year, month or week\n" +
                    "add [id] [y/m/w ]- add certain key for year, month or week\n" + 
                    "remove [id] - delete key from db\n" +
                    "clear - remove all keys from db");
        }

        Console.WriteLine("Exited successfully.");
        Console.ReadLine();
    }

    private static void Loop()
    {
        _server = new Server
        {
            TimeoutTime = ushort.MaxValue // Max value timeout to avoid getting timed out for as long as possible when testing with very high loss rates (if all heartbeat messages are lost during this period of time, it will trigger a disconnection)
        };
        _server.Start(7777, 10);

        while (_running)
        {
            _server.Update();
            Thread.Sleep(10);
        }

        _server.Stop();
    }


    [MessageHandler((ushort)MessageId.RequestAuthorization)]
    private static void RequestAuthorization(ushort fromClientId, Message message)
    {
        string userID = message.GetString();
        string key = message.GetString();

        ushort status = 3;
        KeyStatus keyStatus = null;

        // also check is used, then link user to server.
        if (_keys.ContainsKey(key))
        {
            keyStatus = _keys.Find(key);

            if (keyStatus.userID == userID)
            {
                status = 0;
            }
            else if(keyStatus.userID != "")
            {
                status = 2;
            }
            else
            {
                status = 0;
                _keys.LinkKey(key, userID);
            }
        }
        else
            status = 1;

        ResponseAuthorization(fromClientId, status, keyStatus != null ? keyStatus.expirationTime : DateTime.MinValue);
    }

    private static void ResponseAuthorization(ushort fromClientId, ushort status, DateTime expiration)
    {
        Message message = Message.Create(MessageSendMode.Reliable, MessageId.ResponseAuthorization);
        message.AddUShort(status);
        message.AddLong(expiration.Ticks);

        _server.Send(message, fromClientId);
    }
}

public enum ConnectionStatus : ushort
{
    Connected = 0,
    WrongKey = 1,
    AlreadyUsed = 2,
    ErrorOnServer = 3
}

public enum MessageId : ushort
{
    RequestAuthorization = 0,
    ResponseAuthorization
}
