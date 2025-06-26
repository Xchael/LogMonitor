using LogMonitor;
using LogMonitorData.Models;
using LogMonitorData.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogMonitorTests
{
    public class LogMonitorRunnerTests : IDisposable
    {
        private readonly Mock<ILogParserService> _parserMock;
        private readonly Mock<IJobMonitorService> _monitorMock;
        private readonly IConfiguration _config;
        private readonly LogMonitorRunner _runner;

        private readonly TextWriter _originalConsoleOut;
        private readonly StringWriter _consoleOutWriter;

        public LogMonitorRunnerTests()
        {
            _originalConsoleOut = Console.Out;
            _consoleOutWriter = new StringWriter();
            Console.SetOut(_consoleOutWriter);

            _parserMock = new Mock<ILogParserService>();
            _monitorMock = new Mock<IJobMonitorService>();

            var inMemory = new Dictionary<string, string>
            {
                ["LogMonitor:LogsFolder"] = Path.GetTempPath(),
                ["LogMonitor:LogFileName"] = "dummy.log"
            };
            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            _runner = new LogMonitorRunner(
                _config,
                _parserMock.Object,
                _monitorMock.Object,
                NullLogger<LogMonitorRunner>.Instance
            );
        }

        public void Dispose()
        {
            Console.SetOut(_originalConsoleOut);
        }

        private string GetConsoleOutput() => _consoleOutWriter.ToString();

        [Fact]
        public void Run_NoJobs_Returns0_AndPrintsHeaderOnly()
        {
            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Returns(Enumerable.Empty<LogEntry>());
            _monitorMock
                .Setup(m => m.AnalyzeJobs(It.IsAny<IEnumerable<LogEntry>>()))
                .Returns(Enumerable.Empty<JobInfo>());

            var exitCode = _runner.Run(null);

            Assert.Equal(0, exitCode);

            var lines = GetConsoleOutput()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Single(lines);
            Assert.Equal("Job Report", lines[0]);
        }

        [Fact]
        public void Run_OneJob_Returns0_AndPrintsJobLine()
        {
            var stubEntries = new[]
            {
                new LogEntry
                {
                    TimeStamp      = TimeSpan.Zero,
                    Pid            = 42,
                    JobDescription = "MyJob",
                    IsStart        = true
                }
            };
            var stubJobs = new[]
            {
                new JobInfo
                {
                    PID         = 42,
                    Description = "MyJob",
                    StartTime   = TimeSpan.Zero,
                    EndTime     = TimeSpan.FromMinutes(3)
                }
            };

            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Returns(stubEntries);
            _monitorMock
                .Setup(m => m.AnalyzeJobs(stubEntries))
                .Returns(stubJobs);

            var exitCode = _runner.Run(null);

            Assert.Equal(0, exitCode);

            var lines = GetConsoleOutput()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Job Report", lines[0]);
            Assert.Contains("PID: 42", lines[1]);
            Assert.Contains("MyJob", lines[1]);
            Assert.Matches(@"\d\d:\d\d", lines[1]);
        }

        [Fact]
        public void Run_ParserThrows_Returns1_AndPrintsNothing()
        {
            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Throws(new Exception("parse error"));

            var exitCode = _runner.Run(null);
            var output = GetConsoleOutput();

            Assert.Equal(1, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(output));
        }

        [Fact]
        public void Run_MonitorThrows_Returns1_AndPrintsNothing()
        {
            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Returns(Enumerable.Empty<LogEntry>());
            _monitorMock
                .Setup(m => m.AnalyzeJobs(It.IsAny<IEnumerable<LogEntry>>()))
                .Throws(new Exception("monitor error"));

            var exitCode = _runner.Run(null);
            var output = GetConsoleOutput();

            Assert.Equal(1, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(output));
        }
    }
}
