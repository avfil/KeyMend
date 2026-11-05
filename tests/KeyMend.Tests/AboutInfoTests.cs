using KeyMend.UI.Settings;

namespace KeyMend.Tests;

/// <summary>Тесты <see cref="AboutInfo"/>: адрес репозитория для карточки «О программе».</summary>
public sealed class AboutInfoTests
{
    /// <summary>Адрес — корректная https-ссылка на GitHub: её копируют в буфер и открывают в браузере.</summary>
    [Fact]
    public void RepositoryUrl_Always_IsHttpsGitHubLink()
    {
        var ok = Uri.TryCreate(AboutInfo.RepositoryUrl, UriKind.Absolute, out var uri);

        Assert.True(ok);
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
        Assert.Equal("github.com", uri.Host);
    }
}
