using System.Diagnostics;
using System.Text;

namespace KimoTech.PcapFile.IO.Test
{
    class Program
    {
        static async Task Main(string[] _)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.WriteLine("数据回放测试程序");
                Console.WriteLine("==================");

                // 创建数据目录
                var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
                Console.WriteLine($"\n创建数据目录: {dataDir}");
                try
                {
                    Directory.CreateDirectory(dataDir);
                    Console.WriteLine("数据目录创建成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建数据目录失败: {ex.Message}");
                    return;
                }

                // 运行测试
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "test --no-build --verbosity normal",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine($"错误: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n发生错误: {ex.Message}");
                Console.WriteLine($"错误类型: {ex.GetType().Name}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\n内部错误: {ex.InnerException.Message}");
                }
            }
        }
    }
}
