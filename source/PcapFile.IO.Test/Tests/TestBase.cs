using NUnit.Framework;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    /// <summary>
    /// 测试基类
    /// </summary>
    public abstract class TestBase
    {
        protected string TestDirectory { get; private set; }

        /// <summary>
        /// 测试名称
        /// </summary>
        protected abstract string TestName { get; }

        [OneTimeSetUp]
        public virtual void Setup()
        {
            // 创建测试目录
            var baseDir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(baseDir);
            TestDirectory = Path.Combine(baseDir, GetType().Name);
            Directory.CreateDirectory(TestDirectory);

            WriteTestStart();
        }

        [OneTimeTearDown]
        public virtual void TearDown()
        {
            WriteTestEnd();
        }

        /// <summary>
        /// 输出测试开始信息
        /// </summary>
        protected void WriteTestStart()
        {
            TestContext.WriteLine($"\n开始执行{TestName}...");
            TestContext.WriteLine("=====================================");
        }

        /// <summary>
        /// 输出测试结束信息
        /// </summary>
        protected void WriteTestEnd()
        {
            TestContext.WriteLine($"\n{TestName}完成");
            TestContext.WriteLine("=====================================");
        }

        /// <summary>
        /// 输出测试信息
        /// </summary>
        protected void WriteInfo(string message)
        {
            TestContext.WriteLine(message);
        }

        /// <summary>
        /// 输出测试警告
        /// </summary>
        protected void WriteWarning(string message)
        {
            TestContext.WriteLine($"警告: {message}");
        }

        /// <summary>
        /// 输出测试错误
        /// </summary>
        protected void WriteError(string message)
        {
            TestContext.WriteLine($"错误: {message}");
        }

        /// <summary>
        /// 输出测试异常
        /// </summary>
        protected void WriteException(Exception ex)
        {
            WriteError($"执行{TestName}时发生错误: {ex.Message}");
            WriteError($"错误类型: {ex.GetType().Name}");
            WriteError($"堆栈跟踪: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                WriteError($"内部错误: {ex.InnerException.Message}");
            }
        }
    }
}
