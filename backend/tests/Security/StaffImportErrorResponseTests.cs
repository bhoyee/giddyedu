using System.Text.Json;
using GiddyEdu.Infrastructure.Hr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GiddyEdu.SecurityTests;

public sealed class StaffImportErrorResponseTests
{
    [Fact]
    public async Task OversizedImport_ReturnsClientErrorWithActionableDetail()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var message = "This file contains 501 staff records. Upload at most 500 per file; split larger lists into separate imports.";

        var handled = await handler.TryHandleAsync(context, new StaffImportValidationException(message), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(message, response.RootElement.GetProperty("detail").GetString());
    }
}
