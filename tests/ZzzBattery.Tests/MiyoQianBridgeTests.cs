using System;
using System.IO;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class MiyoQianBridgeTests
{
    [Fact]
    public void SettingsDefaultsEnableMiyoQianBridge()
    {
        var settings = new WidgetSettings();
        Assert.True(settings.MiyoQianEnabled);
        Assert.True(settings.MiyoQianAutoStart);
        Assert.True(settings.MiyoQianSyncWithDailySign);
        Assert.False(settings.MiyoQianHideBar);
    }

    [Fact]
    public void LocatorResolvesMiyoQianFromNestedOutputDirectory()
    {
        var temp = Directory.CreateTempSubdirectory("myq_loc_");
        try
        {
            var miyoDir = Path.Combine(temp.FullName, "MiyoQian");
            Directory.CreateDirectory(miyoDir);
            File.WriteAllText(Path.Combine(miyoDir, "main.py"), "# entry");
            var binDir = Path.Combine(temp.FullName, "dist", "ZZZBattery");
            Directory.CreateDirectory(binDir);

            var resolved = MiyoQianLocator.ResolveProjectDirectory("", binDir);
            Assert.Equal(miyoDir, resolved);
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Fact]
    public void ParserAllowsOnlyLoopbackRuntimeUrls()
    {
        Assert.Equal("http://127.0.0.1:5891", MiyoQianStatusParser.ParseRuntimeUrl("""{"url":"http://127.0.0.1:5891"}"""));
        Assert.Equal("http://[::1]:5890", MiyoQianStatusParser.ParseRuntimeUrl("""{"url":"http://[::1]:5890"}"""));
        Assert.Null(MiyoQianStatusParser.ParseRuntimeUrl("""{"url":"http://user@127.0.0.1:5890"}"""));
        Assert.Null(MiyoQianStatusParser.ParseRuntimeUrl("""{"url":"http://127.0.0.1:5890/evil?x=1"}"""));
        Assert.Null(MiyoQianStatusParser.ParseRuntimeUrl("""{"url":"https://evil.example/"}"""));
        Assert.Null(MiyoQianStatusParser.ParseRuntimeUrl("""{"url":"file:///C:/Windows/calc.exe"}"""));
    }

    [Fact]
    public void ParserMapsCompletedRunningAndFailureStates()
    {
        var completed = MiyoQianStatusParser.Parse(
            enabled: true,
            projectDir: "C:\\MiyoQian",
            url: "http://127.0.0.1:5890",
            reachable: true,
            needAuth: false,
            passwordSet: true,
            statusJson: """{"checkin_summary":{"running":false,"today_completed":true,"today_success":true,"accounts_total":1,"accounts_logged_in":1,"summary":"游戏社区签到汇总：成功 1，失败 0","last_run":"2026-11-28T09:05:00+08:00"}}""");
        Assert.Equal(MiyoQianState.Completed, completed.State);
        Assert.Equal("米游签：今日已完成", completed.BadgeText);
        Assert.True(completed.CanTrigger);

        var partial = MiyoQianStatusParser.Parse(
            enabled: true,
            projectDir: "C:\\MiyoQian",
            url: "http://127.0.0.1:5890",
            reachable: true,
            needAuth: false,
            passwordSet: true,
            statusJson: """{"checkin_summary":{"running":false,"today_completed":true,"today_success":false,"accounts_total":1,"accounts_logged_in":1,"summary":"游戏社区签到汇总：成功 0，失败 1"}}""");
        Assert.Equal(MiyoQianState.PartialFailure, partial.State);
        Assert.Equal("米游签：今日有失败", partial.BadgeText);

        var running = MiyoQianStatusParser.Parse(
            enabled: true,
            projectDir: "C:\\MiyoQian",
            url: "http://127.0.0.1:5890",
            reachable: true,
            needAuth: false,
            passwordSet: true,
            statusJson: """{"checkin_summary":{"running":true,"today_completed":false,"today_success":false,"accounts_total":1,"accounts_logged_in":1}}""");
        Assert.Equal(MiyoQianState.Running, running.State);
        Assert.Equal("米游签：签到中", running.BadgeText);
        Assert.False(running.CanTrigger);

        var needsSetup = MiyoQianStatusParser.Parse(
            enabled: true,
            projectDir: "C:\\MiyoQian",
            url: "http://127.0.0.1:5890",
            reachable: true,
            needAuth: true,
            passwordSet: false,
            statusJson: null);
        Assert.Equal(MiyoQianState.NeedsSetup, needsSetup.State);
        Assert.Equal("米游签：待网页初始化", needsSetup.BadgeText);

        var legacyFallback = MiyoQianStatusParser.Parse(
            enabled: true,
            projectDir: "C:\\MiyoQian",
            url: "http://127.0.0.1:5890",
            reachable: true,
            needAuth: false,
            passwordSet: true,
            statusJson: """{"scheduler":{"running":false,"last_run":""},"logs":[]}""",
            configJson: """{"accounts":[{"name":"demo","stuid":"10001"}]}""");
        Assert.Equal(MiyoQianState.Pending, legacyFallback.State);
        Assert.Equal("米游签：今日待签到", legacyFallback.BadgeText);
        Assert.Equal(1, legacyFallback.AccountsLoggedIn);
    }
}
