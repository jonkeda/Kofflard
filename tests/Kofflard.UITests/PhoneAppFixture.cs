using Kofflard.Core.Models;
using Kofflard.Infrastructure;

namespace Kofflard.UITests;

/// <summary>
/// Kofflard in the phone's narrow layout, whatever the head (.my/design/single-page-phone.md). Hermetic as
/// <see cref="KofflardAppFixture"/>, starting with a new install's lanes, Safe and Curious.
/// </summary>
public sealed class PhoneAppFixture : KofflardAppFixture
{
    /// <inheritdoc />
    protected override KofflardLayout Layout => KofflardLayout.Phone;

    /// <inheritdoc />
    protected override LaneSet StartLanes => LaneSet.Default;
}

/// <summary>Tests against one <see cref="PhoneAppFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class PhoneCollection : ICollectionFixture<PhoneAppFixture>
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Kofflard phone";
}
