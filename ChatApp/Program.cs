using System.Net;
using System.Net.Sockets;

ServerObject server = new ServerObject(); // создасться сервер
await server.ListenAsync(); // запуск сервера

class ServerObject
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // адрес сервера
    List<ClientObject> clients = new List<ClientObject>(); // показывает все подключения
    protected internal void RemoveConnection(string id)
    {
        // получение по ID закрытое подключение
        ClientObject? client = clients.FirstOrDefault(c => c.Id == id);
        // удаление подключения из списка подключений
        if (client != null) clients.Remove(client);
        client?.Close();
    }
    // прослушивание входящих подключений
    protected internal async Task ListenAsync()
    {
        try
        {
            tcpListener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (true)
            {
                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                ClientObject clientObject = new ClientObject(tcpClient, this);
                clients.Add(clientObject);
                Task.Run(clientObject.ProcessAsync);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            Disconnect();
        }
    }
    // отключение всех клиентов
    protected internal void Disconnect()
    {
       foreach (var client in clients)
       {
            client.Close(); // отключает клиента
       }
       tcpListener.Stop(); // остановливает сервер
    }

    // транслирует сообщения подключенным клиентам
    protected internal async Task BroadcastMessage(string message, string id) 
    {
        foreach(var client in clients)
        {
            if(client.Id != id) // проверяет не равно ли ID клиента ID отправителя
            {
                await client.Writer.WriteAsync(message); // передаёт данные
                await client.Writer.FlushAsync();
            }
        }
    }
}
class ClientObject
{
    protected internal string Id { get; } = Guid.NewGuid().ToString();
    protected internal StreamWriter Writer { get;}
    protected internal StreamReader Reader { get;}

    TcpClient client;
    ServerObject server; 

    public ClientObject(TcpClient tcpClient, ServerObject serverObject)
    {
        client = tcpClient;
        server = serverObject;
        var stream = client.GetStream();
        Reader = new StreamReader(stream);
        Writer = new StreamWriter(stream);
    }

    public async Task ProcessAsync()
    {
        try
        {
            // получает имя пользователя
            string? userName = await Reader.ReadLineAsync();
            string? message = $"{userName} подключился!";
            // посылает сообщение всем подключенным клиентам в чате
            await server.BroadcastMessage(message, Id);
            Console.WriteLine(message);
            // получает сообщения от клиента
            while (true)
            {
                try
                {
                    message = await Reader.ReadLineAsync();
                    if (message == null) continue;
                    message = $"{userName}: {message}";
                    await server.BroadcastMessage(message, Id);
                }
                catch
                {
                    message = $"{userName} отключился!";
                    Console.WriteLine(message);
                    await server.BroadcastMessage(message, Id);
                    break;
                }
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            // в случае выхода из цикла закрывает
            server.RemoveConnection(Id);
        }
    }

    protected internal void Close()
    {
        Writer.Close();
        Reader.Close();
        client.Close();
    }
}

