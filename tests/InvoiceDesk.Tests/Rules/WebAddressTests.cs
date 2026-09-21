// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using InvoiceDesk.Core.Rules;

namespace InvoiceDesk.Tests.Rules;

public class WebAddressTests
{
    [Theory]
    [InlineData("https://www.harbourlane.com.au/", "www.harbourlane.com.au")]
    [InlineData("http://harbourlane.com.au", "harbourlane.com.au")]
    [InlineData("harbourlane.com.au/work", "harbourlane.com.au/work")]
    public void display_drops_the_scheme_and_trailing_slash(string input, string expected) =>
        Assert.Equal(expected, WebAddress.Display(input));

    [Theory]
    [InlineData("harbourlane.com.au", "https://harbourlane.com.au")]
    [InlineData("http://harbourlane.com.au", "http://harbourlane.com.au")]
    public void href_always_has_a_scheme(string input, string expected) =>
        Assert.Equal(expected, WebAddress.Href(input));

    [Theory]
    [InlineData("www.harbourlane.com.au", true)]
    [InlineData("https://harbourlane.com.au/contact", true)]
    [InlineData("harbour lane.com.au", false)]
    [InlineData("harbourlane", false)]
    public void validates(string url, bool ok) => Assert.Equal(ok, WebAddress.IsValid(url));
}
