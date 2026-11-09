using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Application;
using Application.Common.Interfaces;
using Application.Dtos.Customers;
using Application.Dtos.Users;
using System.ComponentModel.DataAnnotations;
using Infrastructure;
using Infrastructure.Security;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Npgsql;
using Orders.Workflow;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using WebApi;
using WebApi.Bot;
using WebApi.Infrastructure;

namespace ApiIntegrationTests;

internal static class TestRunner
{
    private static int passed;
    private static int failed;
    private static HttpClient http = null!;
    private static string connectionString = "";
    private static IServiceProvider services = null!;
    private static string rolePermissionsMigration = "";
    private static string workflowMigration = "";

    private static async Task<int> Main()
    {
        // Never read application settings or connect to the deployed database.
        var admin = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 55439, Username = "codex_test", Database = "postgres", Pooling = false
        };
        string database = "codex_pm_tests_" + Guid.NewGuid().ToString("N");
        await using var control = new NpgsqlConnection(admin.ConnectionString);
        await control.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", control).ExecuteNonQueryAsync();
        admin.Database = database;
        connectionString = admin.ConnectionString;
        WebApplication? app = null;
        try
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManagement.slnx"))) directory = directory.Parent;
            if (directory is null) throw new Exception("Repository root not found");
            await Sql(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/01_create_schema.sql")));
            await Sql(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/02_seed_data.sql")));
            await Sql(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/03_auth.sql")));
            await Sql(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/04_telegram.sql")));
            await Sql(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/05_roles.sql")));
            rolePermissionsMigration = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/06_role_permissions.sql"));
            await Sql(rolePermissionsMigration);
            workflowMigration = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "SQL/07_order_workflow.sql"));
            await Sql(workflowMigration);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", Args = [] });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Seed:Passwords:admin"] = "local-admin-seed",
                ["Telegram:BotUsername"] = "OrdersTestBot",
                ["Jwt:Issuer"] = "local-tests", ["Jwt:Audience"] = "local-tests",
                ["Jwt:SigningKey"] = "local-test-key-only-012345678901234567890123456789"
            });
            builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
            builder.Services.AddWebApiSecurity(builder.Configuration).AddBotIntegration(builder.Configuration);
            builder.Services.AddProblemDetails().AddExceptionHandler<ApiExceptionHandler>();
            builder.Services.AddControllers().AddApplicationPart(typeof(WebApi.Controllers.OrdersController).Assembly);
            app = builder.Build();
            services = app.Services;
            await services.SeedUserPasswordsAsync();
            app.UseExceptionHandler();
            app.UseRouting();
            int loginRequests = 0;
            app.Use(async (context, next) =>
            {
                // Keep unrelated credential scenarios in separate rate-limit buckets.
                if (context.Request.Path == "/api/auth/login")
                    context.Connection.RemoteIpAddress = IPAddress.Parse($"192.0.2.{Interlocked.Increment(ref loginRequests)}");
                await next();
            });
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            http = new HttpClient { BaseAddress = new(address), Timeout = TimeSpan.FromSeconds(20) };
            await Run();
        }
        finally
        {
            http?.Dispose();
            if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", control).ExecuteNonQueryAsync();
        }
        Console.WriteLine($"{passed} passed, {failed} failed (isolated PostgreSQL database)");
        return failed == 0 ? 0 : 1;
    }

    private static async Task Test(string name, Func<Task> test)
    {
        try { await test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
    }
    private static async Task<JsonNode> Get(string path) => await Read(await http.GetAsync(path));
    private static async Task<JsonNode> Read(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
            return JsonNode.Parse(body)!;
        }
    }
    private static async Task Status(HttpResponseMessage response, HttpStatusCode expected)
    {
        using (response) Check(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    private static object Draft(JsonNode order, string comment, params object[] items) => new { expectedVersion = (string)order["version"]!, comment, items };
    private static object Item(JsonNode item, int? quantity = null) => new { id = (int)item["id"]!, productId = (int)item["productId"]!, quantity = quantity ?? (int)item["quantity"]! };

    private static async Task<HttpResponseMessage> DeleteRoleAsync(int id, long version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/roles/{id}");
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        return await http.SendAsync(request);
    }

    private static async Task Run()
    {
        await Test("startup seeds configured placeholder passwords without replacing existing hashes", async () =>
        {
            var login = await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "admin", password = "local-admin-seed" }));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)login["accessToken"]!);
            services.GetRequiredService<IConfiguration>()["Seed:Passwords:admin"] = "must-not-replace-existing-password";
            await services.SeedUserPasswordsAsync();
            await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "admin", password = "local-admin-seed" }));
            await Status(await http.PostAsJsonAsync("/api/auth/login", new { login = "admin", password = "must-not-replace-existing-password" }), HttpStatusCode.Unauthorized);
            using var scope = services.CreateScope();
            var pending = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetWithPlaceholderPasswordsAsync();
            Check(pending.All(u => u.Login != "admin") && pending.Any(u => u.Login == "manager"),
                "Configured hashes or unconfigured placeholders changed unexpectedly");
        });

        await Test("legacy password migrates and login/refresh share a rotating token contract", async () =>
        {
            const string password = "previous-format-password";
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
            await Sql($"UPDATE app_users SET password_hash = 'pbkdf2-sha256$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}' WHERE login = 'viewer'");
            var login = await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "viewer", password }));
            var refresh = await Read(await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = (string)login["refreshToken"]! }));
            foreach (var pair in new[] { login, refresh })
            {
                Check(pair["accessToken"] is not null && pair["refreshToken"] is not null &&
                    DateTimeOffset.Parse((string)pair["accessTokenExpiresAt"]!) > DateTimeOffset.UtcNow &&
                    DateTimeOffset.Parse((string)pair["refreshTokenExpiresAt"]!) > DateTimeOffset.UtcNow,
                    "Incomplete token pair");
            }
            Check((string)login["refreshToken"]! != (string)refresh["refreshToken"]!, "Refresh token did not rotate");
            await Status(await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = (string)login["refreshToken"]! }), HttpStatusCode.Unauthorized);
            await Status(await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = (string)refresh["refreshToken"]! }), HttpStatusCode.Unauthorized);

            using var scope = services.CreateScope();
            var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByLoginAsync("viewer");
            Check(user is not null && user.PasswordHash.StartsWith('$'), "Legacy hash did not migrate");
            var admin = http.DefaultRequestHeaders.Authorization;
            try
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)refresh["accessToken"]!);
                var profile = await Get("/api/auth/me");
                Check((string)profile["role"]! == "Viewer" && (string)profile["roleCode"]! == "Viewer" &&
                    (int)profile["id"]! == (int)profile["userId"]! && (string)profile["login"]! == "viewer", "Profile is incompatible with WPF");
                await Get("/api/reports/orders");
                await Status(await http.GetAsync("/api/users"), HttpStatusCode.Forbidden);
                await Status(await http.PostAsJsonAsync("/api/orders/1/status", new { action = "register" }), HttpStatusCode.Forbidden);
            }
            finally { http.DefaultRequestHeaders.Authorization = admin; }
        });

        await Test("Telegram service account can link and report without access to desktop routes", async () =>
        {
            await Read(await http.PostAsJsonAsync("/api/users", new
            {
                login = "telegram-bot", fullName = "Test bot", roleCode = "TelegramBot", password = "local-bot-password"
            }), HttpStatusCode.Created);
            var link = await Read(await http.PostAsync("/api/telegram/link", null));
            var deepLink = new Uri((string)link["url"]!);
            var token = Uri.UnescapeDataString(deepLink.Query["?start=".Length..]);
            const string botPath = "/api/bot/users/987654321";
            await Status(await http.GetAsync(botPath), HttpStatusCode.Forbidden);

            var bot = await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "telegram-bot", password = "local-bot-password" }));
            var admin = http.DefaultRequestHeaders.Authorization;
            try
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)bot["accessToken"]!);
                await Status(await http.GetAsync("/api/orders"), HttpStatusCode.Forbidden);
                await Status(await http.GetAsync("/api/auth/me"), HttpStatusCode.Forbidden);
                await Status(await http.GetAsync(botPath), HttpStatusCode.NotFound);
                await Read(await http.PostAsJsonAsync(botPath + "/link", new { token }));
                var report = await Read(await http.PostAsync(botPath + "/report", null));
                Check((string)report["type"]! == "text" && ((string)report["text"]!).Contains("Звіт для керівника"),
                    "Bot did not return the real manager summary");
                await Status(await http.DeleteAsync(botPath + "/link"), HttpStatusCode.NoContent);
                await Status(await http.PostAsync(botPath + "/report", null), HttpStatusCode.NotFound);
            }
            finally { http.DefaultRequestHeaders.Authorization = admin; }
        });

        await Test("only Admin can manage roles; system roles and last administrator are protected", async () =>
        {
            var admin = await Get("/api/auth/me");
            int adminUserId = (int)admin["id"]!;
            var adminRole = (await Get("/api/roles")).AsArray().Single(r => (string)r!["code"]! == "Admin")!;
            int adminRoleId = (int)adminRole["id"]!;
            long version = (long)adminRole["version"]!;
            Check((bool)adminRole["isSystem"]!, "Seeded Admin role is not protected");
            await Status(await http.PutAsJsonAsync($"/api/roles/{adminRoleId}/active", new { isActive = false, expectedVersion = version }), HttpStatusCode.Conflict);
            await Status(await DeleteRoleAsync(adminRoleId, version), HttpStatusCode.Conflict);
            await Status(await http.PostAsJsonAsync("/api/roles", new { code = "ADMIN", name = "Reserved" }), HttpStatusCode.Conflict);
            await Status(await http.PutAsJsonAsync($"/api/users/{adminUserId}", new
            {
                login = "admin", fullName = "Admin", role = "Admin", isActive = false
            }), HttpStatusCode.Conflict);
            await Status(await http.PutAsJsonAsync($"/api/users/{adminUserId}", new
            {
                login = "admin", fullName = "Admin", role = "Director", isActive = true
            }), HttpStatusCode.Conflict);

            await Read(await http.PostAsJsonAsync("/api/users", new
            {
                login = "role-test-director", fullName = "Director", role = "Director", password = "role-test-director-password"
            }), HttpStatusCode.Created);
            var director = await Read(await http.PostAsJsonAsync("/api/auth/login", new
            {
                login = "role-test-director", password = "role-test-director-password"
            }));
            var adminAuth = http.DefaultRequestHeaders.Authorization;
            try
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)director["accessToken"]!);
                await Status(await http.GetAsync("/api/roles"), HttpStatusCode.Forbidden);
                await Status(await http.PostAsJsonAsync("/api/roles", new { code = "DirectorAttempt", name = "No access" }),
                    HttpStatusCode.Forbidden);
            }
            finally { http.DefaultRequestHeaders.Authorization = adminAuth; }
        });

        await Test("role versioning, blocking and assignment are enforced by the API", async () =>
        {
            var role = await Read(await http.PostAsJsonAsync("/api/roles", new { code = "OpsObserver", name = "Observer" }), HttpStatusCode.Created);
            int id = (int)role["id"]!;
            long firstVersion = (long)role["version"]!;
            await Status(await http.PostAsJsonAsync("/api/roles", new { code = "opsobserver", name = "Duplicate" }), HttpStatusCode.Conflict);
            role = await Read(await http.PutAsJsonAsync($"/api/roles/{id}", new { name = "Operations Observer", expectedVersion = firstVersion }));
            Check((string)role["code"]! == "OpsObserver" && (long)role["version"]! > firstVersion, "Rename changed the code or did not advance the version");
            await Status(await http.PutAsJsonAsync($"/api/roles/{id}", new { name = "Stale", expectedVersion = firstVersion }), HttpStatusCode.Conflict);
            await Status(await http.DeleteAsync($"/api/roles/{id}"), HttpStatusCode.PreconditionRequired);

            await Read(await http.PostAsJsonAsync("/api/users", new
            {
                login = "role-test-user", fullName = "Observer", role = "OpsObserver", password = "role-test-user-password"
            }), HttpStatusCode.Created);
            var login = await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "role-test-user", password = "role-test-user-password" }));
            var admin = http.DefaultRequestHeaders.Authorization;
            try
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)login["accessToken"]!);
                await Get("/api/auth/me");
                await Status(await http.GetAsync("/api/orders"), HttpStatusCode.Forbidden);
            }
            finally { http.DefaultRequestHeaders.Authorization = admin; }

            role = await Read(await http.PutAsJsonAsync($"/api/roles/{id}/active", new { isActive = false, expectedVersion = (long)role["version"]! }));
            Check(!(bool)role["isActive"]!, "Blocked role still marked active");
            try
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)login["accessToken"]!);
                await Status(await http.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
                await Status(await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = (string)login["refreshToken"]! }), HttpStatusCode.Unauthorized);
                await Status(await http.PostAsJsonAsync("/api/auth/login", new { login = "role-test-user", password = "role-test-user-password" }), HttpStatusCode.Unauthorized);
            }
            finally { http.DefaultRequestHeaders.Authorization = admin; }
            await Status(await http.PostAsJsonAsync("/api/users", new
            {
                login = "blocked-assignment", fullName = "Blocked", role = "OpsObserver", password = "blocked-assignment-password"
            }), HttpStatusCode.BadRequest);
            await Status(await DeleteRoleAsync(id, (long)role["version"]!), HttpStatusCode.Conflict);

            await Read(await http.PutAsJsonAsync($"/api/roles/{id}/active", new { isActive = true, expectedVersion = (long)role["version"]! }));
            await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "role-test-user", password = "role-test-user-password" }));
            var unused = await Read(await http.PostAsJsonAsync("/api/roles", new { code = "UnusedRole", name = "Unused" }), HttpStatusCode.Created);
            await Status(await DeleteRoleAsync((int)unused["id"]!, (long)unused["version"]!), HttpStatusCode.NoContent);
        });

        await Test("section visibility is versioned, takes effect for existing JWTs and cannot grant editing", async () =>
        {
            var catalog = (await Get("/api/permissions")).AsArray();
            Check(catalog.Any(p => (string?)p?["code"] == "board.read" && (string?)p?["requires"] == "orders.read"),
                "Board dependency is missing");
            var roles = (await Get("/api/roles")).AsArray();
            int roleId = (int)roles.Single(r => (string?)r?["code"] == "OpsObserver")!["id"]!;
            var matrix = (await Get("/api/roles/permissions")).AsArray();
            var row = matrix.Single(r => (int)r!["roleId"]! == roleId)!;
            long revision = (long)row["revision"]!;
            var user = await Read(await http.PostAsJsonAsync("/api/auth/login", new
            {
                login = "role-test-user", password = "role-test-user-password"
            }));

            await Status(await http.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new
            {
                expectedRevision = revision, permissions = new[] { "roles.manage" }
            }), HttpStatusCode.BadRequest);
            await Status(await http.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new
            {
                expectedRevision = revision, permissions = new[] { "board.read" }
            }), HttpStatusCode.BadRequest);

            var saved = await Read(await http.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new
            {
                expectedRevision = revision, permissions = new[] { "orders.read", "board.read", "reports.read", "products.read" }
            }));
            await Status(await http.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new
            {
                expectedRevision = revision, permissions = Array.Empty<string>()
            }), HttpStatusCode.Conflict);

            var adminAuth = http.DefaultRequestHeaders.Authorization;
            var userAuth = new AuthenticationHeaderValue("Bearer", (string)user["accessToken"]!);
            try
            {
                http.DefaultRequestHeaders.Authorization = userAuth;
                var access = await Get("/api/auth/me/permissions");
                var permissions = access["permissions"]!.AsArray().Select(p => (string)p!).ToArray();
                Check(permissions.Contains("reports.read") && permissions.Contains("board.read") &&
                    !permissions.Contains("orders.edit") && !permissions.Contains("products.manage"), "Read grants escalated writes");
                var orders = (await Get("/api/orders")).AsArray();
                int orderId = (int)orders.First()!["id"]!;
                Check(orders.All(o => (string?)o?["customerFullName"] == ""), "Order list exposes customer identity");
                var details = await Get($"/api/orders/{orderId}");
                Check((int)details["customerId"]! == 0 && details["customerEmail"] is null, "Order detail exposes customer contact");
                await Get("/api/reports/orders");
                await Get("/api/products");
                await Status(await http.GetAsync("/api/customers"), HttpStatusCode.Forbidden);
                await Status(await http.GetAsync("/api/roles/permissions"), HttpStatusCode.Forbidden);
                await Status(await http.PostAsJsonAsync($"/api/orders/{orderId}/status", new { action = "register" }), HttpStatusCode.Forbidden);
                await Status(await http.PutAsJsonAsync("/api/products/1/stock", new { quantity = 0 }), HttpStatusCode.Forbidden);

                http.DefaultRequestHeaders.Authorization = adminAuth;
                await Read(await http.PutAsJsonAsync($"/api/roles/{roleId}/permissions", new
                {
                    expectedRevision = (long)saved["revision"]!, permissions = new[] { "products.read" }
                }));
                var viewer = matrix.Single(r => (int)r!["roleId"]! ==
                    (int)roles.Single(r => (string?)r?["code"] == "Viewer")!["id"]!)!;
                int viewerId = (int)viewer["roleId"]!;
                var originalViewer = viewer["permissions"]!.AsArray().Select(p => (string)p!).ToArray();
                var revokedViewer = await Read(await http.PutAsJsonAsync($"/api/roles/{viewerId}/permissions", new
                {
                    expectedRevision = (long)viewer["revision"]!, permissions = originalViewer.Where(p => p != "reports.read").ToArray()
                }));
                await Sql(rolePermissionsMigration);
                var afterMigration = (await Get("/api/roles/permissions")).AsArray()
                    .Single(r => (int)r!["roleId"]! == viewerId)!;
                Check(afterMigration["permissions"]!.AsArray().All(p => (string?)p != "reports.read"),
                    "Reapplying the seed restored a revoked permission");
                await Read(await http.PutAsJsonAsync($"/api/roles/{viewerId}/permissions", new
                {
                    expectedRevision = (long)revokedViewer["revision"]!, permissions = originalViewer
                }));

                http.DefaultRequestHeaders.Authorization = userAuth;
                await Status(await http.GetAsync("/api/reports/orders"), HttpStatusCode.Forbidden);
                await Status(await http.GetAsync("/api/orders"), HttpStatusCode.Forbidden);
                await Get("/api/products");
            }
            finally { http.DefaultRequestHeaders.Authorization = adminAuth; }
        });

        await Test("unsupported or unconfigured database provider fails before connecting", () =>
        {
            foreach (var settings in new[]
            {
                new Dictionary<string, string?> { ["Database:Provider"] = "Unknown" },
                new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" },
                new Dictionary<string, string?> { ["ConnectionStrings:Default"] = " " }
            })
            {
                bool rejected = false;
                try { new ServiceCollection().AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Invalid persistence configuration was silently accepted");
            }
            return Task.CompletedTask;
        });

        await Test("application transaction rolls back saves across customer and user services", async () =>
        {
            using (var scope = services.CreateScope())
            {
                var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
                var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
                bool aborted = false;
                try
                {
                    await unit.InTransactionAsync<bool>(async ct =>
                    {
                        await customers.SaveAsync(null, new SaveCustomerDto("Rolled back customer", null, "rollback@example.test", null), ct);
                        await users.SaveAsync(null, new SaveUserDto { Login = "rolledback-user", FullName = "Rolled back user", Role = "Viewer" }, "rollback-password", ct);
                        throw new ValidationException("Abort the whole operation.");
                    });
                }
                catch (ValidationException) { aborted = true; }
                Check(aborted, "Expected application rejection");
            }
            Check((await Get("/api/customers")).AsArray().All(c => (string?)c!["email"] != "rollback@example.test"), "Customer escaped transaction rollback");
            Check((await Get("/api/users")).AsArray().All(u => (string)u!["login"]! != "rolledback-user"), "User escaped transaction rollback");
        });

        await Test("order repository refuses an update lock outside a transaction", async () =>
        {
            using var scope = services.CreateScope();
            bool rejected = false;
            try { await scope.ServiceProvider.GetRequiredService<IOrderRepository>().GetForUpdateAsync(1); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Unsafe order update lock was allowed");
        });

        await Test("all five reports translate and execute on PostgreSQL", async () =>
        {
            foreach (string path in new[] { "orders", "orders/today?date=2026-09-01", "products", "low-stock", "manager-summary" })
                await Get("/api/reports/" + path);
            var summary = await Get("/api/reports/orders");
            Check((int)summary["totalOrders"]! > 0 && (decimal)summary["totalAmount"]! > 0, "Missing seeded totals");
            var empty = await Get("/api/reports/orders?from=2099-01-01&to=2099-01-02");
            Check((int)empty["totalOrders"]! == 0 && (decimal)empty["totalAmount"]! == 0, "Date filter ignored");
            Check((await Get("/api/reports/products?from=2099-01-01&to=2099-01-02")).AsArray().Count == 0, "Product period ignored");
            Check((await Get("/api/reports/orders/today?date=2099-01-01"))["orders"]!.AsArray().Count == 0, "Day filter ignored");
            var orders = (await Get("/api/orders")).AsArray();
            Check((decimal)summary["totalAmount"]! == orders.Sum(o => (decimal)o!["totalAmount"]!), "Report totals differ from order totals");
        });

        await Test("product catalog, prices and stock persist through the application interfaces", async () =>
        {
            var product = await Read(await http.PostAsJsonAsync("/api/products", new
            {
                name = "Persistence test product", categoryName = "Persistence test category", price = 12.50m, quantity = 8, isActive = true
            }), HttpStatusCode.Created);
            int id = (int)product["id"]!;
            Check((await Get("/api/products")).AsArray().Any(p => (int)p!["id"]! == id), "Created product missing from catalog");
            await Read(await http.PutAsJsonAsync($"/api/products/{id}", new
            {
                name = "Updated persistence product", categoryName = "Another test category", price = 17.25m, quantity = 6, isActive = false
            }));
            product = await Get($"/api/products/{id}");
            Check((decimal)product["price"]! == 17.25m && (string)product["categoryName"]! == "Another test category" && !(bool)product["isActive"]!,
                "Product changes did not persist");
            await Read(await http.PutAsJsonAsync($"/api/products/{id}/stock", new { quantity = 4 }));
            Check((int)(await Get($"/api/products/{id}/stock"))["quantity"]! == 4, "Stock did not persist");
            await Status(await http.PutAsJsonAsync($"/api/products/{id}/stock", new { quantity = -1 }), HttpStatusCode.BadRequest);
            Check((int)(await Get($"/api/products/{id}/stock"))["quantity"]! == 4, "Rejected stock change persisted");
        });

        int customerId = 0;
        await Test("customer CRUD persists contact details without prior orders", async () =>
        {
            var created = await Read(await http.PostAsJsonAsync("/api/customers", new { fullName = "New customer", phone = "+380123", email = "new@example.test", address = "Address" }), HttpStatusCode.Created);
            customerId = (int)created["id"]!;
            Check((await Get("/api/customers")).AsArray().Any(c => (int)c!["id"]! == customerId), "Missing customer");
            await Read(await http.PutAsJsonAsync($"/api/customers/{customerId}", new { fullName = "Updated customer", email = "updated@example.test", address = "New address" }));
            var saved = await Get($"/api/customers/{customerId}");
            Check((string)saved["address"]! == "New address" && (string)saved["fullName"]! == "Updated customer", "Not persisted");
            await Status(await http.PostAsJsonAsync("/api/customers", new { fullName = "Other", email = "updated@example.test" }), HttpStatusCode.Conflict);
            await Status(await http.PostAsJsonAsync("/api/customers", new { fullName = "", email = "not-email" }), HttpStatusCode.BadRequest);
        });

        int userId = 0;
        await Test("user creation hashes passwords and never returns credentials", async () =>
        {
            var user = await Read(await http.PostAsJsonAsync("/api/users", new { login = "NewUser", fullName = "New user", role = "Manager", password = "initial-password", telegramAllowed = true, telegramUserId = 12345678 }), HttpStatusCode.Created);
            userId = (int)user["id"]!;
            Check(user["password"] is null && user["passwordHash"] is null && (string)user["login"]! == "newuser", "Credentials exposed or login not normalized");
            await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "newuser", password = "initial-password" }));
            Check((await Get("/api/users")).AsArray().All(u => u!["password"] is null && u["passwordHash"] is null), "List exposes credentials");
        });

        await Test("users validate roles, Telegram and duplicate identifiers", async () =>
        {
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "NEWUSER", fullName = "Duplicate", role = "Viewer", password = "safe-test-password" }), HttpStatusCode.Conflict);
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "other", fullName = "Other", role = "Viewer", password = "safe-test-password", telegramUserId = 12345678 }), HttpStatusCode.Conflict);
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "other", fullName = "Other", role = "Unknown", password = "safe-test-password" }), HttpStatusCode.BadRequest);
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "other", fullName = "Other", role = "Viewer", password = "safe-test-password", telegramAllowed = true }), HttpStatusCode.BadRequest);
        });

        await Test("metadata update and separate password replacement persist", async () =>
        {
            await Read(await http.PutAsJsonAsync($"/api/users/{userId}", new { login = "newuser", fullName = "Edited user", role = "Director", isActive = true, telegramAllowed = false }));
            await Status(await http.PutAsJsonAsync($"/api/users/{userId}/password", new { password = "new-password" }), HttpStatusCode.NoContent);
            await Status(await http.PostAsJsonAsync("/api/auth/login", new { login = "newuser", password = "initial-password" }), HttpStatusCode.Unauthorized);
            await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "newuser", password = "new-password" }));
            Check((string)(await Get($"/api/users/{userId}"))["role"]! == "Director", "Role not persisted");
            await Read(await http.PutAsJsonAsync($"/api/users/{userId}", new { login = "newuser", fullName = "Edited user", role = "Director", isActive = false }));
            await Status(await http.PostAsJsonAsync("/api/auth/login", new { login = "newuser", password = "new-password" }), HttpStatusCode.Unauthorized);
        });

        await Test("order creation rejects unknown customer", async () =>
        {
            await Status(await http.PostAsJsonAsync("/api/orders", new { orderNumber = "unknown-customer", customerId = 999999, items = new[] { new { productId = 1, quantity = 1 } } }), HttpStatusCode.BadRequest);
        });

        JsonNode order = null!;
        int orderId = 0;
        await Test("new customer can immediately be used in an order", async () =>
        {
            order = await Read(await http.PostAsJsonAsync("/api/orders", new { orderNumber = "atomic-test", customerId, comment = "original", items = new[] { new { productId = 1, quantity = 2 }, new { productId = 2, quantity = 1 } } }), HttpStatusCode.Created);
            orderId = (int)order["id"]!;
            Check(((string)order["version"]!).Length == 64, "Missing order revision");
        });

        await Test("database duplicate becomes a safe application conflict", async () =>
        {
            using var response = await http.PostAsJsonAsync("/api/orders", new
            {
                orderNumber = "atomic-test", customerId,
                items = new[] { new { productId = 1, quantity = 1 } }
            });
            var body = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == HttpStatusCode.Conflict, $"Expected conflict, got {response.StatusCode}");
            Check(JsonNode.Parse(body)?["detail"] is not null && !body.Contains("Npgsql") && !body.Contains("23505") && !body.Contains("INSERT INTO"),
                "Conflict exposes provider details");
            Check((await Get($"/api/orders/{orderId}")).ToJsonString() == order.ToJsonString(), "Duplicate insert changed the existing order");
        });

        await Test("atomic update changes multiple items and comment preserving historic price and IDs", async () =>
        {
            var first = order["items"]!.AsArray().Single(i => (int)i!["productId"]! == 1)!;
            decimal price = (decimal)first["unitPrice"]!;
            int itemId = (int)first["id"]!;
            string oldVersion = (string)order["version"]!;
            await Sql("UPDATE product_prices SET price = 99 WHERE product_id = 1");
            order = await Read(await http.PutAsJsonAsync($"/api/orders/{orderId}", Draft(order, "atomic comment", Item(first, 3), new { id = 0, productId = 3, quantity = 2 })));
            var updated = order["items"]!.AsArray().Single(i => (int)i!["productId"]! == 1)!;
            Check((int)updated["id"]! == itemId && (decimal)updated["unitPrice"]! == price && (int)updated["quantity"]! == 3, "Existing item was replaced/repriced");
            Check((string)order["comment"]! == "atomic comment" && (string)order["version"]! != oldVersion && order["items"]!.AsArray().Count == 2, "Incomplete update");
        });

        await Test("stale revision returns 409 without overwriting data", async () =>
        {
            await Status(await http.PutAsJsonAsync($"/api/orders/{orderId}", new { expectedVersion = new string('0', 64), comment = "stale", items = order["items"]!.AsArray().Select(i => Item(i!)).ToArray() }), HttpStatusCode.Conflict);
            Check((string)(await Get($"/api/orders/{orderId}"))["comment"]! == "atomic comment", "Stale update persisted");
        });

        await Test("invalid draft cannot partially update comment or quantities", async () =>
        {
            string before = order.ToJsonString();
            await Status(await http.PutAsJsonAsync($"/api/orders/{orderId}", Draft(order, "invalid", Item(order["items"]![0]!, 4), new { id = 0, productId = 999999, quantity = 1 })), HttpStatusCode.BadRequest);
            Check((await Get($"/api/orders/{orderId}")).ToJsonString() == before, "Partial invalid update persisted");
        });

        await Test("database failure rolls back every change in the transaction", async () =>
        {
            await Sql("CREATE FUNCTION fail_test_item() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.product_id = 4 THEN RAISE EXCEPTION 'test rollback'; END IF; RETURN NEW; END $$; CREATE TRIGGER fail_test_item BEFORE INSERT ON order_items FOR EACH ROW EXECUTE FUNCTION fail_test_item();");
            try
            {
                string before = order.ToJsonString();
                await Status(await http.PutAsJsonAsync($"/api/orders/{orderId}", Draft(order, "must rollback", Item(order["items"]![0]!, 4), new { id = 0, productId = 4, quantity = 1 })), HttpStatusCode.InternalServerError);
                Check((await Get($"/api/orders/{orderId}")).ToJsonString() == before, "Transaction was not rolled back");
            }
            finally { await Sql("DROP TRIGGER fail_test_item ON order_items; DROP FUNCTION fail_test_item();"); }
        });

        await Test("two concurrent saves of the same revision have exactly one winner", async () =>
        {
            var items = order["items"]!.AsArray().Select(i => Item(i!)).ToArray();
            var responses = await Task.WhenAll(
                http.PutAsJsonAsync($"/api/orders/{orderId}", Draft(order, "writer A", items)),
                http.PutAsJsonAsync($"/api/orders/{orderId}", Draft(order, "writer B", items)));
            Check(responses.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && responses.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
                "Concurrent save results: " + string.Join(", ", responses.Select(r => r.StatusCode)));
            foreach (var response in responses) response.Dispose();
            order = await Get($"/api/orders/{orderId}");
        });

        await Test("legacy item routes advance revision and preserve nonempty order", async () =>
        {
            string version = (string)order["version"]!;
            await Read(await http.PostAsJsonAsync($"/api/orders/{orderId}/items", new { productId = 4, quantity = 1 }));
            order = await Get($"/api/orders/{orderId}");
            Check((string)order["version"]! != version, "Legacy item write did not change version");
            int item = (int)order["items"]!.AsArray().Single(i => (int)i!["productId"]! == 4)!["id"]!;
            await Status(await http.DeleteAsync($"/api/orders/{orderId}/items/{item}"), HttpStatusCode.NoContent);
            order = await Get($"/api/orders/{orderId}");
        });

        await Test("non-editable status rejects full and legacy item updates", async () =>
        {
            await Read(await http.PostAsJsonAsync($"/api/orders/{orderId}/status", new { action = "register" }));
            order = await Read(await http.PostAsJsonAsync($"/api/orders/{orderId}/status", new { action = "grant" }));
            await Status(await http.PutAsJsonAsync($"/api/orders/{orderId}", Draft(order, "blocked", order["items"]!.AsArray().Select(i => Item(i!)).ToArray())), HttpStatusCode.BadRequest);
            await Status(await http.PostAsJsonAsync($"/api/orders/{orderId}/items", new { productId = 4, quantity = 1 }), HttpStatusCode.BadRequest);
            await Status(await http.DeleteAsync($"/api/orders/{orderId}/items/{order["items"]![0]!["id"]}"), HttpStatusCode.BadRequest);
        });

        await Test("complete order lifecycle and cancellation retain exactly one history entry per transition", async () =>
        {
            var completed = await Read(await http.PostAsJsonAsync("/api/orders", new
            {
                orderNumber = "complete-lifecycle", customerId, items = new[] { new { productId = 1, quantity = 1 } }
            }), HttpStatusCode.Created);
            int completedId = (int)completed["id"]!;
            await Status(await http.PostAsJsonAsync($"/api/orders/{completedId}/status", new { action = "ship" }), HttpStatusCode.BadRequest);
            Check((await Get($"/api/orders/{completedId}")).ToJsonString() == completed.ToJsonString(), "Invalid transition changed the order");
            foreach (var action in new[] { "register", "grant", "ship", "invoice" })
                completed = await Read(await http.PostAsJsonAsync($"/api/orders/{completedId}/status", new { action }));
            Check((string)completed["statusCode"]! == "Invoiced" && completed["statusHistory"]!.AsArray().Count == 5,
                "Lifecycle did not persist its status/history");
            await Status(await http.PostAsJsonAsync($"/api/orders/{completedId}/status", new { action = "cancel" }), HttpStatusCode.BadRequest);
            Check((await Get($"/api/orders/{completedId}")).ToJsonString() == completed.ToJsonString(), "Final status was changed");

            var cancelled = await Read(await http.PostAsJsonAsync("/api/orders", new
            {
                orderNumber = "cancelled-lifecycle", customerId, items = new[] { new { productId = 1, quantity = 1 } }
            }), HttpStatusCode.Created);
            int cancelledId = (int)cancelled["id"]!;
            cancelled = await Read(await http.PostAsJsonAsync($"/api/orders/{cancelledId}/status", new { action = "cancel" }));
            await Status(await http.PostAsJsonAsync($"/api/orders/{cancelledId}/status", new { action = "register" }), HttpStatusCode.BadRequest);
            Check((string)cancelled["statusCode"]! == "Cancelled" && cancelled["statusHistory"]!.AsArray().Count == 2 &&
                (await Get($"/api/orders/{cancelledId}")).ToJsonString() == cancelled.ToJsonString(), "Cancellation history is inconsistent");
        });

        await Test("board actions return the current state and exclude stock-blocked transitions", async () =>
        {
            var created = await Read(await http.PostAsJsonAsync("/api/orders", new
            {
                orderNumber = "board-stock-guard", customerId, items = new[] { new { productId = 1, quantity = 1 } }
            }), HttpStatusCode.Created);
            int id = (int)created["id"]!;
            var registered = await Read(await http.PostAsJsonAsync($"/api/orders/{id}/status", new { action = "register" }));
            string path = $"/api/orders/{id}/transitions";
            int originalStock = (int)(await Get("/api/products/1/stock"))["quantity"]!;
            await Sql("UPDATE product_stocks SET quantity = 0 WHERE product_id = 1");
            try
            {
                var snapshot = await http.GetFromJsonAsync<OrderWorkflowActions>(path) ?? throw new Exception("Actions missing");
                Check(snapshot.StatusCode == "Registered" && snapshot.OrderVersion == (string?)registered["version"] &&
                    snapshot.Actions.Any(action => action.Code == "cancel") && snapshot.Actions.All(action => action.Code != "grant"),
                    "Board exposed a blocked destination or returned a different order snapshot");
                var request = new ExecuteWorkflowTransition(snapshot.WorkflowRevision, snapshot.OrderVersion, null);
                await Status(await http.PostAsJsonAsync(path + "/grant", request), HttpStatusCode.BadRequest);
                Check((await Get($"/api/orders/{id}"))["statusHistory"]!.AsArray().Count == 2, "A rejected drop wrote history");
            }
            finally { await Sql($"UPDATE product_stocks SET quantity = {originalStock} WHERE product_id = 1"); }
            var restored = await http.GetFromJsonAsync<OrderWorkflowActions>(path);
            Check(restored?.Actions.Any(action => action.Code == "grant") == true, "Restored stock did not restore the action");
        });

        await Test("workflow draft, publication and guarded transitions are versioned", async () =>
        {
            const string draftPath = "/api/workflow/draft";
            var admin = http.DefaultRequestHeaders.Authorization;
            var customRole = (await Get("/api/roles")).AsArray().Single(role => (string?)role?["code"] == "OpsObserver")!;
            await Read(await http.PutAsJsonAsync($"/api/roles/{(int)customRole["id"]!}/permissions", new
            {
                expectedRevision = (long)customRole["version"]!, permissions = new[] { "orders.read" }
            }));
            var observer = await Read(await http.PostAsJsonAsync("/api/auth/login", new
            {
                login = "role-test-user", password = "role-test-user-password"
            }));
            var viewer = await Read(await http.PostAsJsonAsync("/api/auth/login", new { login = "viewer", password = "previous-format-password" }));
            var director = await Read(await http.PostAsJsonAsync("/api/auth/login", new
            {
                login = "role-test-director", password = "role-test-director-password"
            }));
            try
            {
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)viewer["accessToken"]!);
                await Get("/api/workflow");
                await Status(await http.GetAsync(draftPath), HttpStatusCode.Forbidden);

                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)director["accessToken"]!);
                var draft = await http.GetFromJsonAsync<WorkflowDraft>(draftPath) ?? throw new Exception("Draft missing");
                var state = new WorkflowState("UnderReview", "На перевірці", 7, "InProgress", false, false, true, "#536BC5");
                var review = new WorkflowTransition("review", "Передати на перевірку", "NewOrder", state.Code,
                    ["Director"], ["CommentRequired"]);
                var finish = new WorkflowTransition("resume", "Зареєструвати", state.Code, "Registered", ["OpsObserver"], []);
                var invalid = new WorkflowDefinition([.. draft.Definition.States, state], [.. draft.Definition.Transitions, review]);
                var invalidResponse = await Read(await http.PutAsJsonAsync(draftPath, new SaveWorkflowDraft(draft.Version, invalid)));
                long invalidVersion = (long)invalidResponse["version"]!;
                var problems = await Read(await http.PostAsJsonAsync(draftPath + "/validate", new PublishWorkflow(invalidVersion, draft.PublishedRevision)));
                Check(problems["errors"]!.AsArray().Count > 0, "Dead-end state passed validation");
                await Status(await http.PostAsJsonAsync(draftPath + "/publish", new PublishWorkflow(invalidVersion, draft.PublishedRevision)),
                    HttpStatusCode.BadRequest);
                Check((long)(await Get("/api/workflow"))["revision"]! == draft.PublishedRevision, "Invalid draft became active");

                var valid = invalid with { Transitions = [.. invalid.Transitions, finish] };
                var saved = await Read(await http.PutAsJsonAsync(draftPath, new SaveWorkflowDraft(invalidVersion, valid)));
                await Status(await http.PutAsJsonAsync(draftPath, new SaveWorkflowDraft(invalidVersion, valid)), HttpStatusCode.Conflict);
                await Read(await http.PostAsJsonAsync(draftPath + "/publish", new PublishWorkflow((long)saved["version"]!, draft.PublishedRevision)));
                var published = await http.GetFromJsonAsync<WorkflowCatalog>("/api/workflow") ?? throw new Exception("Catalog missing");
                Check(published.Revision == draft.PublishedRevision + 1 && published.States.Contains(state), "Published catalog did not contain the new state");

                var created = await Read(await http.PostAsJsonAsync("/api/orders", new
                {
                    orderNumber = "workflow-review", customerId, items = new[] { new { productId = 1, quantity = 1 } }
                }), HttpStatusCode.Created);
                int id = (int)created["id"]!;
                string transitions = $"/api/orders/{id}/transitions";
                var actions = await http.GetFromJsonAsync<OrderWorkflowActions>(transitions) ?? throw new Exception("Actions missing");
                Check(actions.Actions.Any(action => action.Code == review.Code && action.RequiresComment), "Configured action not available");
                var request = new ExecuteWorkflowTransition(actions.WorkflowRevision, actions.OrderVersion, "Checked by Director");

                http.DefaultRequestHeaders.Authorization = admin;
                await Status(await http.PostAsJsonAsync(transitions + "/review", request), HttpStatusCode.Forbidden);
                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)director["accessToken"]!);
                await Status(await http.PostAsJsonAsync(transitions + "/review", request with { Comment = null }), HttpStatusCode.BadRequest);
                await Status(await http.PostAsJsonAsync(transitions + "/review", request with { ExpectedWorkflowRevision = draft.PublishedRevision }),
                    HttpStatusCode.Conflict);
                var results = await Task.WhenAll(http.PostAsJsonAsync(transitions + "/review", request), http.PostAsJsonAsync(transitions + "/review", request));
                Check(results.Count(r => r.StatusCode == HttpStatusCode.OK) == 1 && results.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1,
                    "Repeated transition did not have exactly one winner");
                foreach (var result in results) result.Dispose();
                var moved = await Get($"/api/orders/{id}");
                Check((string)moved["statusCode"]! == state.Code && (bool)moved["canEditItems"]! &&
                    moved["statusHistory"]!.AsArray().Count == 2 &&
                    (string)moved["statusHistory"]![1]!["comment"]! == request.Comment, "Transition or its history was lost");
                await Sql(workflowMigration);
                var afterMigration = await http.GetFromJsonAsync<WorkflowCatalog>("/api/workflow");
                Check(afterMigration?.Revision == published.Revision && afterMigration.States.Contains(state),
                    "Reapplying the migration reset the published workflow");

                http.DefaultRequestHeaders.Authorization = new("Bearer", (string)observer["accessToken"]!);
                var observerActions = await http.GetFromJsonAsync<OrderWorkflowActions>(transitions) ?? throw new Exception("Actions missing");
                Check(observerActions.Actions.Single().Code == "resume", "Custom role did not receive its explicit transition");
                await Status(await http.PutAsJsonAsync($"/api/orders/{id}", Draft(moved, "blocked", Item(moved["items"]![0]!))),
                    HttpStatusCode.Forbidden);
                await Read(await http.PostAsJsonAsync(transitions + "/resume",
                    new ExecuteWorkflowTransition(observerActions.WorkflowRevision, observerActions.OrderVersion, null)));
            }
            finally { http.DefaultRequestHeaders.Authorization = admin; }
        });
    }
}
