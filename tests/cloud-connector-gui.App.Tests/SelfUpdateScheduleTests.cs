using CloudConnectorGui.App;
using Xunit;

namespace CloudConnectorGui.App.Tests;

public sealed class SelfUpdateScheduleTests
{
    [Fact]
    public void IsSelfUpdateCheckDueReturnsFalseWhenDisabled()
    {
        var configuration = new GuiConfiguration
        {
            SelfUpdateCheckInterval = SelfUpdateIntervals.Off
        };

        Assert.False(MainWindowController.IsSelfUpdateCheckDue(configuration));
    }

    [Fact]
    public void IsSelfUpdateCheckDueReturnsTrueWhenNeverChecked()
    {
        var configuration = new GuiConfiguration
        {
            SelfUpdateCheckInterval = SelfUpdateIntervals.Daily
        };

        Assert.True(MainWindowController.IsSelfUpdateCheckDue(configuration));
    }

    [Fact]
    public void IsSelfUpdateCheckDueReturnsFalseBeforeNextWeeklyCheck()
    {
        var configuration = new GuiConfiguration
        {
            SelfUpdateCheckInterval = SelfUpdateIntervals.Weekly,
            LastSelfUpdateCheck = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-6)
        };

        Assert.False(MainWindowController.IsSelfUpdateCheckDue(configuration));
    }
}
