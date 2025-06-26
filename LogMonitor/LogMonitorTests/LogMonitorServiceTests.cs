using LogMonitorData.Models;
using LogMonitorData.Services.Interfaces;
using LogMonitorService;
using LogMonitorService.MonitorUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WorkerTests
{
    public class WorkerTests
    {
        private readonly Mock<ILogParserService> _parserMock;
        private readonly Mock<IJobMonitorService> _monitorMock;
        private readonly Mock<ILogger<Worker>> _loggerMock;
        private readonly Worker _worker;
        private readonly MethodInfo _runIterationMethod;

        public WorkerTests()
        {
            _parserMock = new Mock<ILogParserService>();
            _monitorMock = new Mock<IJobMonitorService>();
            _loggerMock = new Mock<ILogger<Worker>>();

            var settings = new LogMonitorSettings
            {
                LogsFolder = Path.GetTempPath(),
                LogFileName = "dummy.log",
                IntervalHours = 1
            };
            var opts = Options.Create(settings);

            _worker = new Worker(
                _loggerMock.Object,
                _parserMock.Object,
                _monitorMock.Object,
                opts);

            _runIterationMethod = typeof(Worker)
                .GetMethod("RunIterationAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Could not find RunIterationAsync");
        }

        /// <summary>
        /// Helper to invoke the private RunIterationAsync.
        /// </summary>
        private Task InvokeRunIterationAsync(string logFile, CancellationToken ct)
        {
            return (Task)_runIterationMethod.Invoke(_worker, new object[] { logFile, ct })!;
        }

        [Fact]
        public async Task RunIteration_HappyPath_CallsParserAndMonitorOnce()
        {
            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Returns(Array.Empty<LogEntry>());
            _monitorMock
                .Setup(m => m.AnalyzeJobs(It.IsAny<IEnumerable<LogEntry>>()))
                .Returns(Array.Empty<JobInfo>());

            await InvokeRunIterationAsync("anything.log", CancellationToken.None);

            _parserMock.Verify(p => p.ParseLogEntries("anything.log"), Times.Once);
            _monitorMock.Verify(m => m.AnalyzeJobs(It.IsAny<IEnumerable<LogEntry>>()), Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task RunIteration_ParserThrows_LogsErrorAndContinues()
        {
            var ex = new InvalidOperationException("parse failure");
            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Throws(ex);

            var exception = await Record.ExceptionAsync(() =>
                InvokeRunIterationAsync("bad.log", CancellationToken.None));

            Assert.Null(exception);

            _loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(level => level == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("Error during iteration")),
                    It.Is<Exception>(e => e == ex),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RunIteration_MonitorThrows_LogsErrorAndContinues()
        {
            _parserMock
                .Setup(p => p.ParseLogEntries(It.IsAny<string>()))
                .Returns(Array.Empty<LogEntry>());
            var ex = new Exception("monitor failure");
            _monitorMock
                .Setup(m => m.AnalyzeJobs(It.IsAny<IEnumerable<LogEntry>>()))
                .Throws(ex);

            var exception = await Record.ExceptionAsync(() =>
                InvokeRunIterationAsync("good.log", CancellationToken.None));

            Assert.Null(exception);

            _loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(level => level == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("Error during iteration")),
                    It.Is<Exception>(e => e == ex),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
