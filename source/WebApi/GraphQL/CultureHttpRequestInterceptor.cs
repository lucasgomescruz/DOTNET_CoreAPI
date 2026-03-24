using System.Globalization;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;

namespace Project.WebApi.GraphQL;

/// <summary>
/// Applies the same Accept-Language culture resolution used by REST controllers
/// to every GraphQL request, so that IStringLocalizer resolves the correct .resx
/// file for email subjects/bodies and validation messages.
/// </summary>
public sealed class CultureHttpRequestInterceptor : DefaultHttpRequestInterceptor
{
    private static readonly string[] AcceptedCultures = ["en-US", "pt-BR", "es-ES"];

    public override ValueTask OnCreateAsync(
        HttpContext context,
        IRequestExecutor requestExecutor,
        OperationRequestBuilder requestBuilder,
        CancellationToken cancellationToken)
    {
        if (context.Request.Headers.TryGetValue("Accept-Language", out var values))
        {
            var cultureInfo = values.ToString()
                .Split(',')
                .Select(c => c.Trim())
                .Where(AcceptedCultures.Contains)
                .Select(c => new CultureInfo(c))
                .FirstOrDefault() ?? new CultureInfo("en-US");

            CultureInfo.CurrentCulture   = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
        }
        else
        {
            // Default to en-US when no header is present
            CultureInfo.CurrentCulture   = new CultureInfo("en-US");
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        }

        return base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
    }
}
