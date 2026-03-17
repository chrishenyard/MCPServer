using McpServer.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;

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
                options.Authority = keycloakSettings.Authority;
                options.RequireHttpsMetadata = keycloakSettings.RequireHttpsMetadata;
                options.Audience = keycloakSettings.Audience;
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakSettings.Authority,

                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    AudienceValidator = (audiences, token, parameters) =>
                    {
                        if (audiences is null)
                        {
                            return false;
                        }

                        foreach (var aud in audiences)
                        {
                            if (string.IsNullOrWhiteSpace(aud))
                            {
                                continue;
                            }

                            var normalizedAud = aud.TrimEnd('/');

                            // Accept the MCP server URL as audience.
                            if (string.Equals(
                                normalizedAud,
                                normalizedServerUrl,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }

                            // Optionally accept an explicit configured audience too.
                            if (!string.IsNullOrWhiteSpace(keycloakSettings.Audience) &&
                                string.Equals(
                                    aud,
                                    keycloakSettings.Audience,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        logger.LogWarning(
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

                        logger.LogInformation(
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

                        logger.LogInformation(
                            "JWT token validated successfully. Subject: {Subject}",
                            context.Principal?.Identity?.Name ?? "(unknown)");

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
