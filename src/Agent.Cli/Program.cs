using Agent.Core.Cli;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var processor = new CliCommandProcessor();
var result = processor.Execute(args);

if (!string.IsNullOrWhiteSpace(result.StandardOutput))
{
    Console.Out.WriteLine(result.StandardOutput);
}

if (!string.IsNullOrWhiteSpace(result.StandardError))
{
    Console.Error.WriteLine(result.StandardError);
}

return result.ExitCode;
