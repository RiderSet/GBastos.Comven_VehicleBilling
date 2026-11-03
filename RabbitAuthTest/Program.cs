using RabbitMQ.Client;

var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "127.0.0.1";
var portText = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 5672;
var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "";
var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "";
var vhost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") ?? "/";

Console.WriteLine($"Host: {host}");
Console.WriteLine($"Porta: {port}");
Console.WriteLine($"Usuário: {user}");
Console.WriteLine($"VHost: {vhost}");
Console.WriteLine($"Tamanho da senha: {password.Length}");
Console.WriteLine($"Senha contém espaços nas extremidades: {password != password.Trim()}");

var factory = new ConnectionFactory
{
    HostName = host,
    Port = port,
    UserName = user,
    Password = password,
    VirtualHost = vhost
};

try
{
    await using var connection = await factory.CreateConnectionAsync();
    Console.WriteLine("Conexão RabbitMQ estabelecida com sucesso.");
}
catch (Exception ex)
{
    for (Exception? e = ex; e != null; e = e.InnerException)
        Console.WriteLine($"{e.GetType().Name}: {e.Message}");

    Environment.ExitCode = 1;
}
