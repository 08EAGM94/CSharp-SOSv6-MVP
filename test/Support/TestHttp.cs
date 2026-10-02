using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SosMVP.Extensions;

namespace test.Support;

public sealed record TestHttpResponse(int StatusCode, string Body, IHeaderDictionary Headers)
{
    public bool HasHeader(string name)
    {
        return Headers.ContainsKey(name);
    }
}

public static class TestHttp
{
    public static async Task<TestHttpResponse> ExecuteAsync(IResult result)
    {
        var context = CreateContext();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;

        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return new TestHttpResponse(context.Response.StatusCode, body, context.Response.Headers);
    }

    public static async Task<TestHttpResponse> TranslateAsync(Exception exception)
    {
        var context = CreateContext();
        var handler = new ApiExceptionHandler(Microsoft.Extensions.Logging.Abstractions.NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);

        context.Response.Body.Position = 0;

        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return new TestHttpResponse(context.Response.StatusCode, body, context.Response.Headers);
    }

    private static DefaultHttpContext CreateContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var provider = services.BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = provider,
            Response = { Body = new MemoryStream() }
        };
    }
}
