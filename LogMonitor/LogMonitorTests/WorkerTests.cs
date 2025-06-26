using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LogMonitorData.Services;
using LogMonitorData.Models;
using System.Globalization;

namespace LogMonitorTests
{
    public class WorkerTests : IDisposable
    {
        private readonly string _tempFile;
        private readonly LogParserService _service;

        public WorkerTests()
        {
            // Create a temp file for each test
            _tempFile = Path.GetTempFileName();
            _service = new LogParserService(NullLogger<LogParserService>.Instance);
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseLogEntries_InvalidPath_ThrowsArgumentException(string path)
        {
            Assert.Throws<ArgumentException>(() => _service.ParseLogEntries(path).ToList());
        }

        [Fact]
        public void ParseLogEntries_NonexistentFile_ThrowsFileNotFoundException()
        {
            var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Assert.False(File.Exists(missing));
            Assert.Throws<FileNotFoundException>(() => _service.ParseLogEntries(missing).ToList());
        }

        [Fact]
        public void ParseLogEntries_ValidLines_ParsesAllFieldsCorrectly()
        {
            var lines = new[]
            {
                "11:35:23,scheduled task 032, START,37980",
                "11:35:56,background job, END,12345"
            };
            File.WriteAllLines(_tempFile, lines);

            var entries = _service.ParseLogEntries(_tempFile).ToList();
            Assert.Equal(2, entries.Count);

            var first = entries[0];
            Assert.Equal(TimeSpan.Parse("11:35:23"), first.TimeStamp);
            Assert.Equal("scheduled task 032", first.JobDescription);
            Assert.True(first.IsStart);
            Assert.Equal(37980, first.Pid);

            var second = entries[1];
            Assert.Equal(TimeSpan.Parse("11:35:56"), second.TimeStamp);
            Assert.Equal("background job", second.JobDescription);
            Assert.False(second.IsStart);
            Assert.Equal(12345, second.Pid);
        }

        [Fact]
        public void ParseLogEntries_MalformedLines_AreSkipped()
        {
            var lines = new[]
            {
                "",                                // empty
                "too,few,cols",                    // 3 cols only
                "badtime,desc,START,100",          // bad timestamp
                "11:00:00,desc,UNKNOWN,100",       // bad marker
                "11:00:00,desc,START,notint",      // bad PID
                "11:01:00,valid job, END,200"      // one valid
            };
            File.WriteAllLines(_tempFile, lines);

            var entries = _service.ParseLogEntries(_tempFile).ToList();
            Assert.Single(entries);

            var e = entries[0];
            Assert.Equal(TimeSpan.Parse("11:01:00"), e.TimeStamp);
            Assert.Equal("valid job", e.JobDescription);
            Assert.False(e.IsStart);
            Assert.Equal(200, e.Pid);
        }
    }
}