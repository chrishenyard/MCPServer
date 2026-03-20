using McpServer.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Text;

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

        var normalizedServerUrl = keycloakSettings.ServerUrl.TrimEnd('/');

        services
            .AddAuthentication(options =>
            {
                // JWT validates the incoming access token.
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;

                // MCP handles the challenge so VS Code can discover auth metadata.
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;
                options.Audience = keycloakSettings.Audience;
                options.MetadataAddress = keycloakSettings.MetadataAddress;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakSettings.Authority,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(keycloakSettings.ClientSecret!))
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

                        logger.LogDebug(
                            "JWT token validated successfully. Subject: {Subject}",
                            context.Principal?.Identity?.Name ?? "(unknown)");

                        return Task.CompletedTask;
                    },
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        logger.LogDebug(
                            "JWT message received. Path: {Path}, QueryString: {QueryString}",
                            context.HttpContext.Request.Path,
                            context.HttpContext.Request.QueryString);
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
                // This metadata tells VS Code / MCP clients where authorization lives.
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
                .Build());

        return services;
    }
}
