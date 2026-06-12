namespace BazingaGame.Middleware;

/// <summary>
/// Adds baseline security response headers to every request.
/// These mitigate common browser-based attack vectors (MIME sniffing, clickjacking,
/// referrer leakage). TLS/HSTS is handled at the load balancer/ingress layer, so it is
/// intentionally not set here.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Stop the browser from MIME-sniffing a response away from the declared Content-Type.
        headers["X-Content-Type-Options"] = "nosniff";
        // Disallow the API being framed — defends against clickjacking.
        headers["X-Frame-Options"] = "DENY";
        // Don't leak the full URL (which may carry session ids) in the Referer header.
        headers["Referrer-Policy"] = "no-referrer";

        await next(context);
    }
}
