using Kofflard.Infrastructure;
using Kofflard.UITests.Pages;

namespace Kofflard.UITests;

/// <summary>
/// Kofflard showing the phone's design, whatever the head: one page, the search at the bottom
/// (.my/design/single-page-phone.md). Hermetic as <see cref="KofflardAppFixture"/>.
/// </summary>
public sealed class PhoneAppFixture : KofflardAppFixture
{
    /// <inheritdoc />
    protected override KofflardLayout Layout => KofflardLayout.Phone;

    /// <summary>Puts back what earlier tests changed outside the app, and walks the trail back to its start.</summary>
    /// <remarks>The page keeps the last search an earlier test made; a test searches before it looks.</remarks>
    public PhonePage StartPhoneTest()
    {
        Reset();
        return PhonePage.Arrived(Context).WalkTrailBack();
    }

    /// <inheritdoc />
    protected override void Arrived() => PhonePage.Arrived(Context);
}

/// <summary>Tests against one <see cref="PhoneAppFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class PhoneCollection : ICollectionFixture<PhoneAppFixture>
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Kofflard phone";
}
