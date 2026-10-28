using System;
using System.IO;
using System.Text;
using Game.Core.Diagnostics;
using Game.Core.Telemetry;
using Game.Core.Tests.Party;
using NUnit.Framework;

namespace Game.Core.Tests.Telemetry
{
    public class IntakeVectorTests
    {
        private static readonly string VectorPath = Path.Combine("Server", "test-vectors", "intake-samples.json");

        [Test]
        public void ServerIntakeSamplesMatchTheClientWriters()
        {
            var json = Build();
            var path = PartyVectorTests.RepoFile(VectorPath);
            if (Environment.GetEnvironmentVariable(PartyVectorTests.UpdateVariable) == "1")
            {
                File.WriteAllText(path, json);
                Assert.Pass("samples written to " + path);
            }
            Assert.That(File.Exists(path), Is.True, $"run the tests with {PartyVectorTests.UpdateVariable}=1 to create {VectorPath}");
            Assert.That(File.ReadAllText(path).Replace("\r\n", "\n"), Is.EqualTo(json), $"samples are stale, run the tests with {PartyVectorTests.UpdateVariable}=1");
        }

        private static string Build()
        {
            var builder = new StringBuilder("{\n  \"version\": 1,\n  \"telemetry\": [\n");
            builder.Append("    ").Append(TelemetryJson.Write(new TelemetryPacket
            {
                InstallId = "0123456789abcdef0123456789abcdef",
                GameVersion = "0.9.3",
                Session = 4,
                Closed = true,
                SessionSeconds = 5400,
                KeysPerMinute = 182.5,
                ClicksPerMinute = 21.25,
                MouseMeters = 140.75,
                TotalPlaySeconds = 36000,
                FirstPrestigeSeconds = 25200,
                Prestiges = 2,
                RecordStage = 412,
                Character = "voodoo",
                DaysSinceFirst = 7,
                ReturnDays = new[] { 0, 1, 3, 7 }
            }).TrimEnd()).Append(",\n");
            builder.Append("    ").Append(TelemetryJson.Write(new TelemetryPacket
            {
                InstallId = "fedcba9876543210fedcba9876543210",
                GameVersion = "0.9.3",
                Session = 1,
                Closed = false,
                SessionSeconds = 600,
                KeysPerMinute = 95,
                TotalPlaySeconds = 600,
                Character = "viking",
                ReturnDays = new[] { 0 }
            }).TrimEnd()).Append("\n  ],\n  \"errors\": [\n");
            builder.Append("    ").Append(ErrorReportJson.Write(new ErrorReport
            {
                Kind = ErrorReport.CrashKind,
                GameVersion = "0.9.3",
                Os = "Windows 11 (10.0.22631) 64bit",
                CreatedUtc = "2026-10-28T09:15:00Z",
                Message = "NullReferenceException: Object reference not set to an instance of an object",
                StackTrace = "Game.Presentation.Ui.PartyPanel.Refresh (Game.Core.Sim.GameLoop loop)\nGame.Bootstrap.GameStartup.Update ()",
                Lines = new[] { "[12:00:01] loaded save from %USER%", "[12:00:05] party linked" }
            }).TrimEnd()).Append("\n  ]\n}\n");
            return builder.ToString();
        }
    }
}
