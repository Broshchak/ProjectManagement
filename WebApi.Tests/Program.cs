using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Application;
using Infrastructure;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Npgsql;

namespace ApiIntegrationTests;

internal static class TestRunner
{
    private static int passed;
    private static int failed;
    private static HttpClient http = null!;
    private static string connectionString = "";

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
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", Args = [] });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Jwt:Issuer"] = "local-tests", ["Jwt:Audience"] = "local-tests",
                ["Jwt:SigningKey"] = "local-test-key-only-012345678901234567890123456789"
            });
            builder.Services.AddApplication().AddBasicAuthApplication().AddInfrastructure(builder.Configuration).AddBasicAuthInfrastructure(builder.Configuration);
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(WebApi.Controllers.OrdersController).Assembly);
            app = builder.Build();
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

    private static async Task Run()
    {
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
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "NEWUSER", fullName = "Duplicate", role = "Viewer", password = "secret" }), HttpStatusCode.Conflict);
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "other", fullName = "Other", role = "Viewer", password = "secret", telegramUserId = 12345678 }), HttpStatusCode.Conflict);
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "other", fullName = "Other", role = "Unknown", password = "secret" }), HttpStatusCode.BadRequest);
            await Status(await http.PostAsJsonAsync("/api/users", new { login = "other", fullName = "Other", role = "Viewer", password = "secret", telegramAllowed = true }), HttpStatusCode.BadRequest);
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
    }
}
