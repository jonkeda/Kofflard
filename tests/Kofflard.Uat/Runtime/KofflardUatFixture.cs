using Bruinet.Core.Composition;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Testing;
using Bruinet.Uat;
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
public sealed class KofflardUatFixture : KofflardAppFixture, IUatScenarioLifecycle
{
    /// <summary>Builds the composition the UAT runtime reads for page discovery and construction.</summary>
    public KofflardUatFixture()
    {
        Composition = TestComposition.ForFixture(this, services => services.AddSingleton<IMauiTestContext>(Context));
    }

    /// <summary>The page graph and DI scope the UAT runtime resolves controls through.</summary>
    public TestComposition Composition { get; }

    /// <inheritdoc />
    public void BeforeFile(BddDocument document)
    {
    }

    /// <inheritdoc />
    public void BeforeScenario(BddBoundScenario scenario) => StartTest();
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
/// scenario.
/// </summary>
[Collection(KofflardUatCollection.CollectionName)]
[Trait("Category", "UAT")]
[Trait("Target", "MAUI")]
public sealed class KofflardUatScenarioTests(KofflardUatFixture fixture) : UatScenarioTestBase<KofflardUatFixture>(fixture)
{
    /// <summary>One case per scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    /// <summary>Runs one scenario and asserts it passes.</summary>
    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <inheritdoc />
    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "KofflardUatFixture");
}
