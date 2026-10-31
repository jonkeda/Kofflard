using Kofflard.Core.Models;

namespace Kofflard.Core.Rules;

/// <summary>
/// What of an answer still being written can be shown (.my/design/streaming-answers.md): the searched
/// musician, then each pick that passes the rules, up to its lane's share.
/// </summary>
/// <remarks>
/// A pick shown here is always in the final result: the result takes each lane's share in the model's order,
/// as this does, and only adds to it (<see cref="SuggestionValidator"/>). Picks are held back until the
/// answer says it is "ok", so nothing shows for a name the model did not know.
/// </remarks>
public sealed class StreamedAnswer(string artist, IReadOnlyCollection<string> exclude, LaneMix? mix = null)
{
    private readonly LaneMix _mix = mix ?? LaneMix.Even;
    private readonly DiscoveryAnswerReader _reader = new();
    private readonly PickFilter _filter = new(artist, exclude);
    private readonly Dictionary<Lane, int> _shown = [];
    private readonly List<DiscoveryAnswer.Pick> _waiting = [];
    private string? _status;

    /// <summary>Whether a pick has been shown.</summary>
    public bool HasShown => _shown.Count > 0;

    /// <summary>Adds the next piece of the answer and returns what can now be shown, in order.</summary>
    public IReadOnlyList<DiscoveryUpdate> Add(string? chunk)
    {
        var updates = new List<DiscoveryUpdate>();
        foreach (var part in _reader.Read(chunk))
        {
            switch (part)
            {
                case DiscoveryAnswerReader.StatusPart status:
                    _status = status.Status;
                    if (_status == "ok")
                    {
                        _waiting.ForEach(pick => Show(pick, updates));
                    }

                    _waiting.Clear();
                    break;
                case DiscoveryAnswerReader.ArtistPart { Artist.Name: { Length: > 0 } name } found:
                    _filter.Searched(name);
                    updates.Add(new DiscoveryUpdate.ArtistFound(new Musician(name.Trim(), string.IsNullOrWhiteSpace(found.Artist.Summary) ? null : found.Artist.Summary.Trim())));
                    break;
                case DiscoveryAnswerReader.PickPart { Pick: var pick }:
                    if (_status is null)
                    {
                        _waiting.Add(pick);
                    }
                    else if (_status == "ok")
                    {
                        Show(pick, updates);
                    }

                    break;
            }
        }

        return updates;
    }

    private void Show(DiscoveryAnswer.Pick pick, List<DiscoveryUpdate> updates)
    {
        if (_filter.Accept(pick) is not { } suggestion)
        {
            return;
        }

        var shown = _shown.GetValueOrDefault(suggestion.Lane);
        if (shown < _mix.Count(suggestion.Lane))
        {
            _shown[suggestion.Lane] = shown + 1;
            updates.Add(new DiscoveryUpdate.PickFound(suggestion));
        }
    }
}
