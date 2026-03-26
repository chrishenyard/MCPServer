using McpServer.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using Serilog;
using System.Diagnostics;
using System.Security.Claims;

namespace McpServer.Configuration;

public static class AuthExtensions
{
    public static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        var keycloakSettings = config
            .GetSection(KeycloakSettings.Section)
            .Get<KeycloakSettings>()
            ?? throw new InvalidOperationException(
                $"Missing configuration section '{KeycloakSettings.Section}'.");

        if (string.IsNullOrWhiteSpace(keycloakSettings.Authority))
        {
            throw new InvalidOperationException("Keycloak Authority is required.");
        }

        if (string.IsNullOrWhiteSpace(keycloakSettings.ServerUrl))
        {
            throw new InvalidOperationException("Keycloak ServerUrl is required.");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;

                // Let MCP expose auth metadata/challenge behavior.
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;
                options.Authority = keycloakSettings.Authority;
                options.MetadataAddress = keycloakSettings.MetadataAddress;
                options.SaveToken = false;
                options.BackchannelHttpHandler = new BackChannelListener();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakSettings.Authority,

                    ValidateAudience = true,
                    ValidAudience = keycloakSettings.Audience,

                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "scope"
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        logger.LogDebug(
                            context.Exception,
                            "JWT authentication failed. Path: {Path}",
                            context.HttpContext.Request.Path);

                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        logger.LogDebug(
                            "JWT challenge triggered. Path: {Path}, Error: {Error}, Description: {Description}",
                            context.HttpContext.Request.Path,
                            context.Error,
                            context.ErrorDescription);

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        var identity = context.Principal?.Identity as ClaimsIdentity;

                        if (identity is not null)
                        {
                            var scopeClaims = identity.FindAll("scope").ToList();

                            foreach (var scopeClaim in scopeClaims)
                            {
                                var scopes = scopeClaim.Value.Split(
                                    ' ',
                                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                                foreach (var scope in scopes)
                                {
                                    identity.AddClaim(new Claim(identity.RoleClaimType, scope));
                                }
                            }
                        }

                        logger.LogDebug(
                            "JWT token validated successfully. Subject: {Subject}",
                            context.Principal?.Identity?.Name ?? "(unknown)");

                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");
                        logger.LogDebug(
                            "JWT forbidden response triggered. Path: {Path}",
                            context.HttpContext.Request.Path);
                        return Task.CompletedTask;
                    }
                };
            })
            .AddMcp(options =>
            {
                options.ResourceMetadata = new()
                {
                    Resource = keycloakSettings.ServerUrl,
                    AuthorizationServers = [keycloakSettings.Authority],
                    ScopesSupported =
                    {
                        "openid",
                        "profile",
                        "email",
                        "mcp:tools"
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy("McpTools", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var scopeClaims = context.User.FindAll("scope").Select(c => c.Value);

                    return scopeClaims
                        .SelectMany(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Contains("mcp:tools");
                });
            });

        return services;
    }
}

// Source - https://stackoverflow.com/a/79287062
// Posted by Tore Nestenius
// Retrieved 2026-03-25, License - CC BY-SA 4.0

public class BackChannelListener : DelegatingHandler
{
    public BackChannelListener() : base(new HttpClientHandler())
    {
    }

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                 CancellationToken token)
    {
        var sw = new Stopwatch();
        sw.Start();

        try
        {
            var result = await base.SendAsync(request, token);
            sw.Stop();

            Log.Logger.ForContext("SourceContext", "BackChannelListener")
                       .Information($"### BackChannel request to {request?.RequestUri?.AbsoluteUri} took {sw.ElapsedMilliseconds.ToString()} ms");

            return result;
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("SourceContext", "BackChannelListener")
                       .Error(ex, $"### BackChannel request to {request?.RequestUri?.AbsoluteUri} failed after {sw.ElapsedMilliseconds.ToString()} ms with exception: {ex.Message}");
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        {
            RequestMessage = request,
            Content = new StringContent("An error occurred while processing the backchannel request.")
        };
    }
}
