using Bruinet.Maui.CommunityToolkit.Controls.Layouts;
using Kofflard.UITests.Pages;

namespace Kofflard.UITests.Containers;

/// <summary>
/// The results page's <c>StateContainer</c>: the suggestions, or Loading, Candidates, Empty or Error.
/// </summary>
public sealed class ResultsState : StateContainer<ResultsPage, ResultsState>
{
    /// <summary>The view while the AI answers.</summary>
    public const string Loading = "Results_Loading";

    /// <summary>The suggestions.</summary>
    public const string Content = "Results_Items";

    /// <summary>Who the user may have meant.</summary>
    public const string Candidates = "Results_Candidates";

    /// <summary>Nobody usable was suggested.</summary>
    public const string Empty = "Results_Empty";

    /// <summary>The request failed.</summary>
    public const string Error = "Results_Error";

    /// <summary>Creates the state container within the results page.</summary>
    public ResultsState(IMauiScope<ResultsPage> parentScope, string automationId)
        : base(parentScope, automationId)
    {
    }

    /// <inheritdoc />
    protected override string StateViewId(string state) => state switch
    {
        nameof(Loading) => Loading,
        nameof(Content) => Content,
        nameof(Candidates) => Candidates,
        nameof(Empty) => Empty,
        nameof(Error) => Error,
        _ => throw new ArgumentException($"Unknown results state '{state}'. Use Loading, Content, Candidates, Empty or Error.", nameof(state)),
    };
}
