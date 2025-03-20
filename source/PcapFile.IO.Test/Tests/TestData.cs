using System.Text;

namespace KimoTech.PcapFile.IO.Test.Tests
{
    /// <summary>
    /// 测试数据管理类
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// 测试数据目录
        /// </summary>
        public static string TestDirectory => Path.Combine(AppContext.BaseDirectory, "data");

        /// <summary>
        /// 示例数据包负载
        /// </summary>
        public static readonly byte[] SamplePayload = Encoding.UTF8.GetBytes("这是一个测试数据包");

        /// <summary>
        /// 测试文件路径
        /// </summary>
        public static string TestFilePath => Path.Combine(TestDirectory, "test_data.pcap");

        /// <summary>
        /// 大文件测试路径
        /// </summary>
        public static string LargeTestFilePath => Path.Combine(TestDirectory, "large_test.pcap");

        /// <summary>
        /// 获取测试目录
        /// </summary>
        public static string GetTestDirectory(string testName)
        {
            var dir = Path.Combine(TestDirectory, testName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
