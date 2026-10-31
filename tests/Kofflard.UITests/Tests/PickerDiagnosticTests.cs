using System.Diagnostics;
using Kofflard.UITests.Pages;
using Xunit.Abstractions;

namespace Kofflard.UITests.Tests;

/// <summary>
/// Regression for the picker hang (.my/rca/picker-hang.md): times each phase of a selection, on Connect and
/// on Settings, with a watchdog so a hang fails in 30 s instead of stopping the run. Before the fix the
/// Connect selection hung in about 60% of Windows runs; run it ten times after touching the Picker mapping.
/// </summary>
[Collection(KofflardCollection.Name)]
[Trait("Category", "UITest")]
[Trait("Rca", "picker-hang")]
public sealed class PickerDiagnosticTests(KofflardAppFixture fixture, ITestOutputHelper output)
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    [Fact]
    public void Settings_OpenWith()
    {
        var settings = fixture.StartTest().OpenSettings();
        Phase("open", () => settings.OpenWith.OpenFlyout());
        Phase("read items", () => output.WriteLine("   items: " + string.Join(" | ", settings.OpenWith.GetDropdownItemTexts() ?? [])));
        Phase("close", () => settings.OpenWith.CloseFlyout());
        Phase("select Spotify", () => settings.OpenWith.SelectByText("Spotify"));
        Phase("read selected", () => output.WriteLine("   selected: " + settings.OpenWith.GetSelectedText()));
    }

    [Fact]
    public void Connect_Provider()
    {
        var settings = fixture.StartTest().OpenSettings();
        settings.ChangeButton.Click();
        var connect = ConnectPage.Arrived(fixture.Context);
        Phase("read selected", () => output.WriteLine("   selected: " + connect.Provider.GetSelectedText()));
        Phase("open", () => connect.Provider.OpenFlyout());
        Phase("read items", () => output.WriteLine("   items: " + string.Join(" | ", connect.Provider.GetDropdownItemTexts() ?? [])));
        Phase("close", () => connect.Provider.CloseFlyout());
        Phase("select OpenAI", () => connect.Provider.SelectByText("OpenAI"));
        Phase("read selected", () => output.WriteLine("   selected: " + connect.Provider.GetSelectedText()));
        Phase("read model", () => output.WriteLine("   model: " + connect.Model.GetText()));
    }

    // Runs one phase on its own thread; a phase still running after the watchdog is reported as hung.
    private void Phase(string name, Action action)
    {
        var watch = Stopwatch.StartNew();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        }) { IsBackground = true };
        thread.Start();

        if (!thread.Join(Watchdog))
        {
            output.WriteLine($"{name}: HUNG (still running after {Watchdog.TotalSeconds:0} s)");
            throw new TimeoutException($"Phase '{name}' hung.");
        }

        if (failure is not null)
        {
            throw new InvalidOperationException($"Phase '{name}' failed after {watch.ElapsedMilliseconds} ms.", failure);
        }

        output.WriteLine($"{name}: {watch.ElapsedMilliseconds} ms{(failure is null ? string.Empty : $" FAILED {failure.GetType().Name}: {failure.Message.Split('\n')[0]}")}");
    }
}
