using System.Text;
using System.Text.Json;

namespace Kofflard.Core.Rules;

/// <summary>
/// Reads the model's answer while it is still being written (.my/design/streaming-answers.md): given the
/// text as it arrives, it reports each part of <see cref="DiscoveryPrompt.Schema"/> once that part is
/// complete. Half a pick is never reported.
/// </summary>
/// <remarks>
/// It scans for complete JSON values instead of parsing the whole, so it needs no closing brace. Anything
/// before the first "{", such as a Markdown fence, is skipped. The whole answer is still parsed at the end
/// (<see cref="DiscoveryAnswer.Parse"/>); this only shows things sooner.
/// </remarks>
public sealed class DiscoveryAnswerReader
{
    private readonly StringBuilder _text = new();
    private readonly List<Level> _levels = [];
    private int _position;
    private bool _inString;
    private bool _escaped;
    private int _stringStart;
    private int _valueStart = -1;
    private bool _finished;

    /// <summary>One complete part of the answer.</summary>
    public abstract record Part;

    /// <summary>The answer's "status": "ok", "ambiguous" or "unknown".</summary>
    public sealed record StatusPart(string Status) : Part;

    /// <summary>The musician searched for, as the model knows them.</summary>
    public sealed record ArtistPart(DiscoveryAnswer.Person Artist) : Part;

    /// <summary>One suggestion, as the model wrote it.</summary>
    public sealed record PickPart(DiscoveryAnswer.Pick Pick) : Part;

    /// <summary>Adds the next piece of text and returns the parts it completed, in order.</summary>
    public IReadOnlyList<Part> Read(string? chunk)
    {
        var parts = new List<Part>();
        if (string.IsNullOrEmpty(chunk) || _finished)
        {
            return parts;
        }

        _text.Append(chunk);
        for (; _position < _text.Length && !_finished; _position++)
        {
            Step(_text[_position], parts);
        }

        return parts;
    }

    private void Step(char c, List<Part> parts)
    {
        if (_inString)
        {
            if (_escaped)
            {
                _escaped = false;
            }
            else if (c == '\\')
            {
                _escaped = true;
            }
            else if (c == '"')
            {
                _inString = false;
                EndString(_text.ToString(_stringStart + 1, _position - _stringStart - 1), parts);
            }

            return;
        }

        if (_levels.Count == 0 && c != '{')
        {
            return;
        }

        switch (c)
        {
            case '"':
                _inString = true;
                _stringStart = _position;
                break;
            case '{':
            case '[':
                if (StartsWatchedValue())
                {
                    _valueStart = _position;
                }

                _levels.Add(new Level(c == '{'));
                break;
            case '}':
            case ']':
                _levels.RemoveAt(_levels.Count - 1);
                if (_levels.Count == 0)
                {
                    _finished = true;
                }
                else if (_valueStart >= 0 && EndsWatchedValue())
                {
                    Complete(_text.ToString(_valueStart, _position - _valueStart + 1), parts);
                    _valueStart = -1;
                }

                break;
            case ':':
                Top.AwaitingValue = true;
                break;
            case ',':
                if (Top.IsObject)
                {
                    Top.AwaitingValue = false;
                }

                break;
        }
    }

    private Level Top => _levels[^1];

    // The root's "artist" object, and each object in the root's "suggestions" array.
    private bool StartsWatchedValue()
        => (_levels.Count == 1 && _levels[0].Key == "artist" && _levels[0].AwaitingValue)
           || (_levels.Count == 2 && _levels[0].Key == "suggestions" && !_levels[1].IsObject);

    private bool EndsWatchedValue() => _levels.Count is 1 or 2;

    private void EndString(string text, List<Part> parts)
    {
        if (_levels.Count == 0 || !Top.IsObject)
        {
            return;
        }

        if (!Top.AwaitingValue)
        {
            Top.Key = text;
        }
        else if (_levels.Count == 1 && Top.Key == "status")
        {
            parts.Add(new StatusPart(text));
        }
    }

    private void Complete(string json, List<Part> parts)
    {
        try
        {
            if (_levels.Count == 1 && JsonSerializer.Deserialize<DiscoveryAnswer.Person>(json) is { } artist)
            {
                parts.Add(new ArtistPart(artist));
            }
            else if (_levels.Count == 2 && JsonSerializer.Deserialize<DiscoveryAnswer.Pick>(json) is { } pick)
            {
                parts.Add(new PickPart(pick));
            }
        }
        catch (JsonException)
        {
            // A part the model got wrong is left to the whole answer's parse at the end.
        }
    }

    private sealed class Level(bool isObject)
    {
        public bool IsObject { get; } = isObject;

        public string? Key { get; set; }

        public bool AwaitingValue { get; set; }
    }
}
