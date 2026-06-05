namespace DicomViewer.WorkflowScpDemo;

internal sealed record WorkflowServerOptions(string Host, int Port, string CalledAeTitle, string DataFilePath)
{
    public static WorkflowServerOptions Parse(string[] args)
    {
        var host = "127.0.0.1";
        var port = 11112;
        var calledAeTitle = "RIS_SCP";
        var dataFilePath = Path.Combine(AppContext.BaseDirectory, "workflow-data.json");

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "--host":
                    host = ReadValue(args, ref index, "--host");
                    break;
                case "--port":
                    port = int.Parse(ReadValue(args, ref index, "--port"));
                    break;
                case "--ae":
                    calledAeTitle = ReadValue(args, ref index, "--ae");
                    break;
                case "--data":
                    dataFilePath = Path.GetFullPath(ReadValue(args, ref index, "--data"));
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"不支持的参数：{args[index]}");
            }
        }

        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "端口必须在 1-65535 范围内。");
        }

        return new WorkflowServerOptions(host, port, calledAeTitle, dataFilePath);
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{optionName} 缺少参数值。");
        }

        index++;
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("用法:");
        Console.WriteLine("  dotnet run --project tests/DicomViewer.WorkflowScpDemo -- [--host 127.0.0.1] [--port 11112] [--ae RIS_SCP] [--data workflow-data.json]");
    }
}
