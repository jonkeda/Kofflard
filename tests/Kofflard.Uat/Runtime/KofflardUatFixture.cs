using Bruinet.Core.Composition;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Testing;
using Bruinet.Bdd;
using Bruinet.Uat;
using Kofflard.Infrastructure;
using Kofflard.Uat.Phrases;
using Kofflard.UITests;
using Kofflard.UITests.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Kofflard.Uat.Runtime;

/// <summary>
/// Kofflard, hermetic, presented to the UAT engine as its root, as Todo's UAT fixture is. Each scenario
/// starts on the search page with the fake AI reset, no history and default settings.
/// </summary>
/// <remarks>
/// <see cref="Composition"/> is where the engine discovers the page objects and phrases (from
/// <c>[TestModuleScan]</c>) and how it constructs them (the scope resolves <see cref="IMauiTestContext"/>).
/// </remarks>
[TestModuleScan(typeof(SearchPage), NamespacePrefix = "Kofflard.UITests.Pages")]
[TestModuleScan(typeof(AiPhrases), NamespacePrefix = "Kofflard.Uat.Phrases")]
[UatPlatforms(MauiPlatform.Windows, MauiPlatform.Android)]
public class KofflardUatFixture : KofflardAppFixture, IUatScenarioLifecycle
{
    /// <summary>Builds the composition the UAT runtime reads for page discovery and construction.</summary>
    public KofflardUatFixture()
    {
        // The phrases take KofflardUatFixture; the phone's fixture is one too.
        Composition = TestComposition.ForFixture(this, services => services
            .AddSingleton<IMauiTestContext>(Context)
            .AddSingleton(this));
    }

    /// <summary>The page graph and DI scope the UAT runtime resolves controls through.</summary>
    public TestComposition Composition { get; }

    /// <inheritdoc />
    public void BeforeFile(BddDocument document)
    {
    }

    /// <inheritdoc />
    public void BeforeScenario(BddBoundScenario scenario) => StartScenario();

    /// <summary>Puts back what earlier scenarios changed, and returns to the first page.</summary>
    protected virtual void StartScenario() => StartTest();
}

/// <summary>
/// Kofflard in the phone's narrow layout (.my/design/single-page-phone.md), for the scenarios tagged
/// <c>@phone</c>.
/// </summary>
public sealed class KofflardPhoneUatFixture : KofflardUatFixture
{
    /// <inheritdoc />
    protected override KofflardLayout Layout => KofflardLayout.Phone;
}

/// <summary>Binds the phone scenarios to the one <see cref="KofflardPhoneUatFixture"/>.</summary>
[CollectionDefinition(CollectionName)]
public sealed class KofflardPhoneUatCollection : ICollectionFixture<KofflardPhoneUatFixture>
{
    /// <summary>The collection name of the phone scenarios.</summary>
    public const string CollectionName = "Kofflard UAT phone";
}

/// <summary>Which design a scenario is for: <c>@phone</c> runs on the phone's, everything else on the desktop's.</summary>
internal static class KofflardScenarios
{
    /// <summary>The tag of the phone's scenarios.</summary>
    public const string PhoneTag = "phone";

    /// <summary>One case per scenario for the phone (<paramref name="phone"/>) or the desktop design.</summary>
    public static IEnumerable<object[]> For(bool phone)
        => BddScenarioSource.EnumerateScenarios()
            .Where(scenario => scenario.ScenarioName.Length > 0
                && scenario.Tags.Any(tag => BddTagConventions.NormalizeTag(tag).Equals(PhoneTag, StringComparison.OrdinalIgnoreCase)) == phone)
            .Select(scenario => new object[] { scenario.FilePath, scenario.ScenarioName });
}

/// <summary>Binds every UAT scenario class to the one <see cref="KofflardUatFixture"/>.</summary>
[CollectionDefinition(CollectionName)]
public sealed class KofflardUatCollection : ICollectionFixture<KofflardUatFixture>
{
    /// <summary>The collection name shared by the UAT scenario classes.</summary>
    public const string CollectionName = "Kofflard UAT";
}

/// <summary>
/// Runs every scenario of every <c>Features/*.feature.md</c> against the hermetic app, one test case per
/// scenario; the <c>@phone</c> ones run in <see cref="KofflardPhoneUatScenarioTests"/>.
/// </summary>
[Collection(KofflardUatCollection.CollectionName)]
[Trait("Category", "UAT")]
[Trait("Target", "MAUI")]
public sealed class KofflardUatScenarioTests(KofflardUatFixture fixture) : UatScenarioTestBase<KofflardUatFixture>(fixture)
{
    /// <summary>One case per scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => KofflardScenarios.For(phone: false);

    /// <summary>Runs one scenario and asserts it passes.</summary>
    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <inheritdoc />
    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "KofflardUatFixture");
}

/// <summary>Runs the <c>@phone</c> scenarios against the app showing the phone's design.</summary>
[Collection(KofflardPhoneUatCollection.CollectionName)]
[Trait("Category", "UAT")]
[Trait("Target", "MAUI")]
[Trait("Layout", "phone")]
public sealed class KofflardPhoneUatScenarioTests(KofflardPhoneUatFixture fixture) : UatScenarioTestBase<KofflardPhoneUatFixture>(fixture)
{
    /// <summary>One case per <c>@phone</c> scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => KofflardScenarios.For(phone: true);

    /// <summary>Runs one scenario and asserts it passes.</summary>
    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <inheritdoc />
    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "KofflardUatFixture");
}
