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

    /// <summary>
    /// Puts back what earlier tests changed outside the app, and walks the trail back to its start, so a
    /// test's searches make the only trail.
    /// </summary>
    /// <remarks>The page keeps the last search an earlier test made; a test searches before it looks.</remarks>
    public PhonePage StartPhoneTest()
    {
        Reset();
        var page = PhonePage.Arrived(Context);
        for (var step = 0; step < 20 && page.BackButton.IsExists(); step++)
        {
            page.BackButton.Click();
            page.BackButton.WaitExists(false, TestConstants.PageTimeoutMs / 10);
        }

        return page;
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
