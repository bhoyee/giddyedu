using System.Text.Json;
using GiddyEdu.Infrastructure.Hr;
using GiddyEdu.Infrastructure.StudentLifecycle;
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

    [Fact]
    public async Task GuardianWithStudentLinks_ReturnsSafeConflictDetail()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var exception = new GuardianStudentLinksExistException(3, "moving the guardian to the bin");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Guardian has linked students", response.RootElement.GetProperty("title").GetString());
        Assert.Equal(exception.Message, response.RootElement.GetProperty("detail").GetString());
    }
}
