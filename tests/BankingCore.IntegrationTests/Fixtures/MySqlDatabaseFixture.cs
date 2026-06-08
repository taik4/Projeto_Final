using System.Diagnostics;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using Testcontainers.MySql;
using MySqlConnector;
using Dapper;

namespace BankingCore.IntegrationTests.Fixtures;

/// <summary>
/// Fixture que gerencia o ciclo de vida de um container MySQL para testes de integração.
/// Implementa IAsyncLifetime para executar código assíncrono antes e depois dos testes.
/// </summary>
public class MySqlDatabaseFixture : IAsyncLifetime
{
    private const string DatabaseName = "banking_core";
    private const string RootPassword = "TestPassword123!";

    // Usa o construtor com imagem para evitar warning CS0618
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.0")
        .WithDatabase(DatabaseName)
        .WithPassword(RootPassword)
        .WithCleanUp(true)
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var baseConnectionString = _container.GetConnectionString();
        
        // Adiciona Allow User Variables=true para suportar variáveis de sessão MySQL
        // necessárias para os OUT parameters da stored procedure
        ConnectionString = $"{baseConnectionString};Allow User Variables=true";

        // Aguarda o MySQL estar pronto
        await WaitForDatabaseReadyAsync();

        // Executa o script init.sql usando o CLI mysql dentro do container
        // (suporta DELIMITER nativamente, diferente do MySqlConnector)
        await ExecuteInitScriptViaCliAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public MySqlConnection CreateConnection()
    {
        return new MySqlConnection(ConnectionString);
    }

    private async Task WaitForDatabaseReadyAsync()
    {
        using var connection = CreateConnection();
        var maxAttempts = 30;
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            try
            {
                await connection.OpenAsync();
                connection.Close();
                return;
            }
            catch
            {
                attempts++;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("MySQL container did not become ready in time");
    }

    private async Task ExecuteInitScriptViaCliAsync()
    {
        // Localiza o arquivo init.sql
        var solutionDir = FindSolutionDirectory();
        var initSqlPath = Path.Combine(solutionDir, "db", "init.sql");

        if (!File.Exists(initSqlPath))
        {
            throw new FileNotFoundException($"init.sql not found at: {initSqlPath}");
        }

        var sqlScript = await File.ReadAllTextAsync(initSqlPath);

        // Remove comandos CREATE USER, GRANT e FLUSH PRIVILEGES pois o container
        // Testcontainers já conecta como root com todos os privilégios necessários.
        sqlScript = Regex.Replace(sqlScript, @"CREATE USER[^;]*;", "", RegexOptions.IgnoreCase);
        sqlScript = Regex.Replace(sqlScript, @"GRANT[^;]*;", "", RegexOptions.IgnoreCase);
        sqlScript = Regex.Replace(sqlScript, @"FLUSH PRIVILEGES[^;]*;", "", RegexOptions.IgnoreCase);

        // Copia o script para dentro do container e executa via CLI mysql
        var containerId = _container.Id;
        var containerScriptPath = "/tmp/init.sql";

        // Escreve o script em um arquivo temporário
        var tempScriptPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempScriptPath, sqlScript);

        try
        {
            // Copia o arquivo para o container usando docker cp
            var copyResult = await RunProcessAsync("docker", $"cp \"{tempScriptPath}\" {containerId}:{containerScriptPath}");
            if (copyResult.ExitCode != 0)
                throw new Exception($"Falha ao copiar init.sql para container: {copyResult.Error}");

            // Executa o script via CLI mysql dentro do container
            var execResult = await RunProcessAsync("docker",
                $"exec {containerId} mysql -u root -p{RootPassword} {DatabaseName} -e \"source {containerScriptPath}\"");

            if (execResult.ExitCode != 0)
                throw new Exception($"Falha ao executar init.sql: {execResult.Error}");
        }
        finally
        {
            if (File.Exists(tempScriptPath))
                File.Delete(tempScriptPath);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find solution directory");
    }
}
