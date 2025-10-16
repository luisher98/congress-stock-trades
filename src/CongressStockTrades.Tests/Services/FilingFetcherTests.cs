using CongressStockTrades.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace CongressStockTrades.Tests.Services;

public class FilingFetcherTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<FilingFetcher>> _loggerMock;
    private readonly FilingFetcher _filingFetcher;

    public FilingFetcherTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<FilingFetcher>>();
        _filingFetcher = new FilingFetcher(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetLatestFilingAsync_WithValidHtml_ShouldReturnFiling()
    {
        // Arrange
        var htmlResponse = GetSampleHtmlResponse();
        SetupHttpResponse(htmlResponse, HttpStatusCode.OK);

        // Act
        var result = await _filingFetcher.GetLatestFilingAsync(2025);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("20250123456");
        result.Name.Should().Be("Doe, John");
        result.Office.Should().Be("CA12");
        result.FilingYear.Should().Be("2025");
        result.PdfUrl.Should().Contain("20250123456.pdf");
    }

    [Fact]
    public async Task GetFilingsAsync_WithMultipleFilings_ShouldReturnSortedByIdDescending()
    {
        // Arrange
        var htmlResponse = GetMultipleFilingsHtml();
        SetupHttpResponse(htmlResponse, HttpStatusCode.OK);

        // Act
        var results = await _filingFetcher.GetFilingsAsync(2025);

        // Assert
        results.Should().HaveCountGreaterThan(1);
        // Verify they are sorted by converting IDs to long for comparison
        var sortedResults = results.Select(f => long.Parse(f.Id)).ToList();
        sortedResults.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetFilingsAsync_WithNonPtrFilings_ShouldFilterThem()
    {
        // Arrange
        var htmlResponse = GetHtmlWithNonPtrFilings();
        SetupHttpResponse(htmlResponse, HttpStatusCode.OK);

        // Act
        var results = await _filingFetcher.GetFilingsAsync(2025);

        // Assert
        results.Should().OnlyContain(f => f.PdfUrl.Contains("ptr-pdfs"));
    }

    [Fact]
    public async Task GetLatestFilingAsync_WithNoFilings_ShouldReturnNull()
    {
        // Arrange
        var htmlResponse = GetEmptyHtmlResponse();
        SetupHttpResponse(htmlResponse, HttpStatusCode.OK);

        // Act
        var result = await _filingFetcher.GetLatestFilingAsync(2025);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFilingsAsync_WithHttpError_ShouldReturnEmptyList()
    {
        // Arrange
        SetupHttpResponse("", HttpStatusCode.InternalServerError);

        // Act
        var results = await _filingFetcher.GetFilingsAsync(2025);

        // Assert
        results.Should().BeEmpty();
    }

    private void SetupHttpResponse(string content, HttpStatusCode statusCode)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private static string GetSampleHtmlResponse()
    {
        return @"
            <html>
                <body>
                    <table>
                        <tbody>
                            <tr>
                                <td data-label='Name'><a href='/public_disc/ptr-pdfs/2025/20250123456.pdf'>Doe, John</a></td>
                                <td data-label='Office'>CA12</td>
                                <td data-label='Filing Year'>2025</td>
                                <td data-label='Filing'>PTR</td>
                            </tr>
                        </tbody>
                    </table>
                </body>
            </html>";
    }

    private static string GetMultipleFilingsHtml()
    {
        return @"
            <html>
                <body>
                    <table>
                        <tbody>
                            <tr>
                                <td data-label='Name'><a href='/public_disc/ptr-pdfs/2025/20250123456.pdf'>Doe, John</a></td>
                                <td data-label='Office'>CA12</td>
                                <td data-label='Filing Year'>2025</td>
                                <td data-label='Filing'>PTR</td>
                            </tr>
                            <tr>
                                <td data-label='Name'><a href='/public_disc/ptr-pdfs/2025/20250100001.pdf'>Smith, Jane</a></td>
                                <td data-label='Office'>NY05</td>
                                <td data-label='Filing Year'>2025</td>
                                <td data-label='Filing'>PTR</td>
                            </tr>
                        </tbody>
                    </table>
                </body>
            </html>";
    }

    private static string GetHtmlWithNonPtrFilings()
    {
        return @"
            <html>
                <body>
                    <table>
                        <tbody>
                            <tr>
                                <td data-label='Name'><a href='/public_disc/ptr-pdfs/2025/20250123456.pdf'>Doe, John</a></td>
                                <td data-label='Office'>CA12</td>
                                <td data-label='Filing Year'>2025</td>
                                <td data-label='Filing'>PTR</td>
                            </tr>
                            <tr>
                                <td data-label='Name'><a href='/public_disc/financial-pdfs/2025/20250123457.pdf'>Smith, Jane</a></td>
                                <td data-label='Office'>NY05</td>
                                <td data-label='Filing Year'>2025</td>
                                <td data-label='Filing'>FD</td>
                            </tr>
                        </tbody>
                    </table>
                </body>
            </html>";
    }

    private static string GetEmptyHtmlResponse()
    {
        return @"
            <html>
                <body>
                    <table>
                        <tbody>
                        </tbody>
                    </table>
                </body>
            </html>";
    }
}
