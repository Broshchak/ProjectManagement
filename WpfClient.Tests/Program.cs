using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfClient.Views;
using WpfClient;
using WpfClient.Models;
using WpfClient.Services;
using WpfClient.ViewModels;

internal static class Program
{
    private static int failures;
    private static int passed;

    [STAThread]
    private static int Main()
    {
        new App().InitializeComponent();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(async () =>
        {
            try { await Run(); }
            catch (Exception ex) { Console.Error.WriteLine(ex); failures++; }
            finally
            {
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
        });
        Dispatcher.Run();
        Console.WriteLine($"{passed} passed, {failures} failed");
        return failures == 0 ? 0 : 1;
    }

    private static async Task Test(string name, Func<Task> test)
    {
        try { await test(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex); failures++; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Set(object target, string name, object? value) =>
        target.GetType().GetProperty(name)!.SetValue(target, value);

    private static Task<T> Call<T>(object target, string name) =>
        (Task<T>)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null)!;

    private static Task Call(object target, string name) =>
        (Task)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null)!;

    private static MainViewModel ViewModel(HttpOrdersApiClient client, string role = "Admin")
    {
        MainViewModel vm = new(new ApiClientSettings { UseMockData = false }, client);
        vm.ReportDateFrom = new(2026, 10, 1);
        vm.ReportDateTo = new(2026, 10, 25);
        vm.TodayReportDate = new(2026, 10, 25);
        Set(vm, "IsAuthenticated", true);
        Set(vm, "CurrentRoleName", role);
        return vm;
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string ItemJson = """{"id":10,"productId":1,"productName":"Real product","quantity":2,"unitPrice":20,"currencyCode":"EUR","lineTotal":40}""";
    private static string Detail(int id) => $$"""{"id":{{id}},"orderNumber":"O-{{id}}","customerId":1,"customerFullName":"Actual customer","statusCode":"NewOrder","isStatusFinal":false,"currencyCode":"EUR","totalAmount":40,"items":[{{ItemJson}}],"statusHistory":[],"customerPhone":"123","updatedAt":"2026-10-25T09:00:00Z"}""";
    private const string Orders = """[{"id":1,"customerId":1,"customerFullName":"Actual customer","statusCode":"NewOrder","currencyCode":"EUR","totalAmount":40},{"id":2,"customerId":42,"customerFullName":"Other actual customer","statusCode":"NewOrder","totalAmount":10}]""";
    private const string Products = """[{"id":1,"name":"Real product","categoryName":"Real category","price":20,"quantity":100,"currencyCode":"EUR","isActive":true}]""";

    private static HttpResponseMessage Reports(string path) => path.Split('?')[0] switch
    {
        "/api/reports/orders" => Json("""{"totalOrders":3,"totalAmount":120,"averageOrderAmount":40}"""),
        "/api/reports/orders/today" => Json("""{"date":"2026-10-25T00:00:00Z","totalOrders":1,"totalAmount":40,"orders":[]}"""),
        "/api/reports/products" => Json("""[{"productId":1,"productName":"Real product","categoryName":"Real","quantity":6,"totalAmount":120}]"""),
        "/api/reports/low-stock" => Json("[]"),
        "/api/reports/manager-summary" => Json("""{"generatedAt":"2026-10-25T10:00:00Z","text":"Actual manager report"}"""),
        _ => Json("{}", HttpStatusCode.NotFound)
    };

    private static async Task Run()
    {
        await Test("invalid configuration never enables mock mode", () =>
        {
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "{ broken");
                var settings = ApiClientSettings.Load(file);
                Check(!settings.UseMockData && settings.ConfigurationError is not null, "Invalid configuration silently accepted");
                File.WriteAllText(file, """{"ApiClient":{"BaseUrl":"file:///secret","TimeoutSeconds":0}}""");
                Check(ApiClientSettings.Load(file).ConfigurationError is not null, "Invalid URL accepted");
                Check(!ApiClientSettings.Load(file + ".missing").UseMockData, "Missing file enables demo mode");
            }
            finally { File.Delete(file); }
            return Task.CompletedTask;
        });

        await Test("404 product catalog does not return invented products", async () =>
        {
            using Fake server = new(_ => Json("{}", HttpStatusCode.NotFound));
            try { await server.Client.GetProductsAsync(); throw new Exception("Expected 404"); }
            catch (ApiException ex) { Check(ex.StatusCode == HttpStatusCode.NotFound, "Lost status"); }
        });

        await Test("missing order returns null and non-object errors preserve HTTP status", async () =>
        {
            using Fake server = new(_ => Json("[]", HttpStatusCode.NotFound));
            Check(await server.Client.GetOrderDetailsAsync(999) is null, "Missing order did not return null");
            try { await server.Client.GetProductsAsync(); throw new Exception("Expected 404"); }
            catch (ApiException ex) { Check(ex.StatusCode == HttpStatusCode.NotFound, "Lost HTTP error status"); }
        });

        await Test("validation problem includes field errors and status", async () =>
        {
            using Fake server = new(_ => Json("""{"title":"Validation failed","errors":{"Quantity":["Must be positive"]}}""", HttpStatusCode.BadRequest));
            try { await server.Client.CreateOrderAsync(new()); throw new Exception("Expected 400"); }
            catch (ApiException ex)
            {
                Check(ex.StatusCode == HttpStatusCode.BadRequest && ex.Message.Contains("Must be positive"), "Validation details lost");
            }
        });

        await Test("non-JSON server failure never exposes stack trace", async () =>
        {
            using Fake server = new(_ => new(HttpStatusCode.InternalServerError) { Content = new StringContent("private stack trace") });
            try { await server.Client.GetOrdersReportAsync(); throw new Exception("Expected failure"); }
            catch (ApiException ex) { Check(!ex.Message.Contains("private") && ex.StatusCode == HttpStatusCode.InternalServerError, "Leaked server output"); }
        });

        await Test("login preserves identity, expiration and Bearer authentication", async () =>
        {
            using Fake server = new(r => r.RequestUri!.AbsolutePath.EndsWith("login")
                ? Json("""{"accessToken":"test-token","accessTokenExpiresAt":"2099-10-25T12:00:00Z","tokenType":"Bearer"}""")
                : Json("""{"id":42,"login":"custom","fullName":"Actual user","role":"Director"}"""));
            var user = await server.Client.SignInAsync("custom", "password");
            Check(user.Id == 42 && user.AccessTokenExpiresAt?.Year == 2099, "Lost identity/expiration");
            Check(server.Authorization.Last() == "Bearer test-token", "Token not sent to me endpoint");
            server.Client.SignOut();
            Check(server.Http.DefaultRequestHeaders.Authorization is null, "Token retained after signout");
        });

        await Test("expired token is rejected before another protected request", async () =>
        {
            using Fake server = new(_ => Json("""{"accessToken":"expired","accessTokenExpiresAt":"2000-01-01T00:00:00Z"}"""));
            bool expired = false;
            server.Client.SessionExpired += () => expired = true;
            try { await server.Client.SignInAsync("custom", "password"); throw new Exception("Expected expiration"); }
            catch (ApiException ex) { Check(ex.StatusCode == HttpStatusCode.Unauthorized, "Wrong error"); }
            Check(expired && server.Paths.Count == 1 && server.Http.DefaultRequestHeaders.Authorization is null, "Expired token used");
        });

        await Test("quantity replacement uses a single versioned PUT and never DELETE/POST", async () =>
        {
            using Fake server = new(_ => Json(Detail(1)));
            await server.Client.ReplaceOrderItemAsync(1, 10, new() { ProductId = 1, Quantity = 3 });
            Check(server.Methods.SequenceEqual(new[] { "GET", "PUT" }), "Unsafe replacement requests");
            Check(server.Bodies.Last()!.Contains("expectedVersion") && server.Bodies.Last()!.Contains("quantity"), "Missing versioned draft");
        });

        await Test("successful report snapshot keeps data and requested period together", async () =>
        {
            using Fake server = new(r => Reports(r.RequestUri!.PathAndQuery));
            var snapshot = await ReportSnapshot.LoadAsync(server.Client, new(2026, 10, 1), new(2026, 10, 25), new(2026, 10, 25));
            Check(snapshot.Summary.TotalOrders == 3 && snapshot.ProductSales.Single().Quantity == 6, "Wrong data");
            Check(snapshot.From.Day == 1 && snapshot.To.Day == 25 && snapshot.ManagerText == "Actual manager report", "Wrong period/text");
        });

        await Test("report query dates are invariant under Thai culture", async () =>
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");
                using Fake server = new(r => Reports(r.RequestUri!.PathAndQuery));
                await server.Client.GetOrdersReportAsync(new(2026, 10, 1), new(2026, 10, 25));
                await server.Client.GetTodayOrdersReportAsync(new(2026, 10, 25));
                Check(server.Paths[0].Contains("from=2026-10-01") && server.Paths[1].Contains("date=2026-10-25"), "Calendar changed API dates");
            }
            finally { CultureInfo.CurrentCulture = culture; }
        });

        await Test("failed report refresh clears old result and prevents PDF dialog", async () =>
        {
            bool fail = false;
            using Fake server = new(r => fail && r.RequestUri!.AbsolutePath.EndsWith("products")
                ? Json("{}", HttpStatusCode.InternalServerError) : Reports(r.RequestUri!.PathAndQuery));
            var vm = ViewModel(server.Client);
            Check(await Call<bool>(vm, "LoadReportsAsync") && vm.HasLoadedReports, "Initial report failed");
            fail = true;
            Check(!await Call<bool>(vm, "LoadReportsAsync") && !vm.HasLoadedReports && vm.ProductSalesReport.Count == 0, "Old report retained");
            // This would block on a file dialog if the export incorrectly continued after failure.
            await Call(vm, "ExportReportsToPdfAsync");
        });

        await Test("changing filters clears displayed report and invalid range makes no request", async () =>
        {
            using Fake server = new(r => Reports(r.RequestUri!.PathAndQuery));
            var vm = ViewModel(server.Client);
            await Call<bool>(vm, "LoadReportsAsync");
            vm.ReportDateFrom = new(2026, 10, 26);
            vm.ReportDateTo = new(2026, 10, 25);
            int count = server.Paths.Count;
            Check(!vm.HasLoadedReports && !await Call<bool>(vm, "LoadReportsAsync") && server.Paths.Count == count, "Invalid range fetched or displayed");
        });

        await Test("late report response cannot overwrite changed filters", async () =>
        {
            TaskCompletionSource gate = new();
            using Fake server = new(async (r, _) => { await gate.Task; return Reports(r.RequestUri!.PathAndQuery); });
            var vm = ViewModel(server.Client);
            Task<bool> pending = Call<bool>(vm, "LoadReportsAsync");
            vm.ReportDateFrom = vm.ReportDateFrom.AddDays(1);
            gate.SetResult();
            Check(!await pending && !vm.HasLoadedReports, "Stale response published");
        });

        await Test("role matrix exposes Director product management and Manager product view", () =>
        {
            using Fake server = new(_ => Json("[]"));
            var director = ViewModel(server.Client, "Director");
            Check(director.CanManageProducts && !director.CanManageUsers, "Wrong director rights");
            Set(director, "CurrentRoleName", "Manager");
            Check(director.NavigationItems.Any(i => i.Key == "Products") && !director.CanViewReports, "Wrong manager rights");
            Set(director, "CurrentRoleName", "Admin");
            Check(!director.CanManageUsers, "Real user editor still writes demo users");
            return Task.CompletedTask;
        });

        await Test("real customers override demo names and only selected details load", async () =>
        {
            using Fake server = new(r => r.RequestUri!.AbsolutePath switch
            {
                "/api/orders" => Json(Orders),
                "/api/products" => Json(Products),
                "/api/orders/1" => Json(Detail(1)),
                _ => Json("{}", HttpStatusCode.NotFound)
            });
            var vm = ViewModel(server.Client, "Manager");
            await Call(vm, "LoadClientDataAsync");
            Check(vm.Customers.Count == 2 && vm.Customers.Single(c => c.Id == 1).FullName == "Actual customer", "Demo customers leaked");
            Check(!server.Paths.Any(p => p.StartsWith("/api/reports")) && !server.Paths.Contains("/api/orders/2"), "Unnecessary reports/details fetched");
            var details = await server.Client.GetOrderDetailsAsync(1);
            Check(details!.CurrencyCode == "EUR" && details.Items.Single().CurrencyCode == "EUR" && details.CustomerPhone == "123", "Dropped API detail fields");
        });

        await Test("catalog failure clears unavailable products instead of demo fallback", async () =>
        {
            using Fake server = new(r => r.RequestUri!.AbsolutePath == "/api/orders" ? Json("[]") : Json("{}", HttpStatusCode.InternalServerError));
            var vm = ViewModel(server.Client, "Manager");
            vm.Products.Add(new() { Id = 999 });
            await Call(vm, "LoadClientDataAsync");
            Check(vm.Products.Count == 0 && vm.ApiErrorMessage is not null, "Retained writable stale products");
        });

        await Test("management APIs expose customers without orders and real users", async () =>
        {
            using Fake server = new(r => r.RequestUri!.AbsolutePath switch
            {
                "/api/orders" => Json("[]"),
                "/api/products" => Json(Products),
                "/api/customers" => Json("""[{"id":77,"fullName":"Customer without orders","email":"real@example.test","address":"Real address"}]"""),
                "/api/users" => Json("""[{"id":99,"login":"actual","fullName":"Actual user","role":"Manager","isActive":true,"telegramAllowed":false}]"""),
                _ => Reports(r.RequestUri!.PathAndQuery)
            });
            var vm = ViewModel(server.Client);
            await Call(vm, "LoadClientDataAsync");
            Check(vm.Customers.Single().Id == 77 && vm.Users.Single().Id == 99 && vm.Users.Single().Password == "", "Management data not loaded");
            Check(vm.IsCustomerManagementAvailable && vm.CanManageUsers, "Management commands unavailable after successful loading");
        });

        await Test("user metadata and password use separate API requests", async () =>
        {
            const string userJson = """{"id":99,"login":"actual","fullName":"Actual user","role":"Manager","isActive":true,"telegramAllowed":false}""";
            using Fake server = new(r => r.RequestUri!.AbsolutePath switch
            {
                "/api/orders" => Json("[]"),
                "/api/products" => Json(Products),
                "/api/customers" => Json("[]"),
                "/api/users" => Json("[" + userJson + "]"),
                "/api/users/99" => Json(userJson),
                "/api/users/99/password" => new(HttpStatusCode.NoContent),
                _ => Reports(r.RequestUri!.PathAndQuery)
            });
            var vm = ViewModel(server.Client);
            await Call(vm, "LoadClientDataAsync");
            vm.BeginEditUserCommand.Execute(null);
            Check(vm.UserPassword == "", "Existing password exposed");
            vm.UserFullName = "Changed name";
            await Call(vm, "SaveUserAsync");
            int metadata = server.Paths.FindIndex(p => p == "/api/users/99");
            Check(metadata >= 0 && !server.Bodies[metadata]!.Contains("password", StringComparison.OrdinalIgnoreCase), "Metadata carries password");
            vm.BeginEditUserCommand.Execute(null);
            vm.UserPassword = "new-password";
            await Call(vm, "ChangeUserPasswordAsync");
            Check(server.Paths.Last() == "/api/users/99/password" && vm.UserPassword == "", "Separate password operation failed");
        });

        await Test("one atomic client save includes item changes and comment", async () =>
        {
            using Fake server = new(r => r.RequestUri!.AbsolutePath switch
            {
                "/api/orders/1" => Json(Detail(1)),
                "/api/orders" => Json("[]"),
                "/api/products" => Json(Products),
                "/api/customers" => Json("[]"),
                "/api/users" => Json("[]"),
                _ => Reports(r.RequestUri!.PathAndQuery)
            });
            var vm = ViewModel(server.Client);
            vm.Products.Add(new() { Id = 1, Quantity = 100, IsActive = true });
            vm.Products.Add(new() { Id = 2, Quantity = 100, IsActive = true });
            Set(vm, "SelectedOrderDetails", new OrderDetail { Id = 1, Version = new string('a', 64), Status = "NewOrder", Items = [new() { Id = 10, ProductId = 1, Quantity = 2 }] });
            vm.EditableOrderItems.Add(new() { Id = 10, ProductId = 1, Quantity = 3 });
            vm.EditableOrderItems.Add(new() { ProductId = 2, Quantity = 1 });
            vm.OrderComment = "Atomic comment";
            Check(await vm.SaveOrderEditorAsync(), "Atomic save failed: " + vm.OrderItemEditorError);
            int index = server.Paths.FindIndex(p => p == "/api/orders/1");
            Check(server.Methods.Count(m => m == "PUT") == 1 && !server.Methods.Contains("DELETE") && !server.Methods.Contains("POST"), "Save split into mutations");
            Check(server.Bodies[index]!.Contains("Atomic comment") && server.Bodies[index]!.Contains("expectedVersion"), "Missing comment/revision");
        });

        await Test("late detail response cannot replace the currently selected order", async () =>
        {
            TaskCompletionSource gate = new();
            using Fake server = new(async (r, _) =>
            {
                if (r.RequestUri!.AbsolutePath == "/api/orders/1") await gate.Task;
                return Json(Detail(r.RequestUri!.AbsolutePath.EndsWith("1") ? 1 : 2));
            });
            var vm = ViewModel(server.Client, "Manager");
            vm.SelectedOrder = new() { Id = 1 };
            vm.SelectedOrder = new() { Id = 2 };
            await Call(vm, "LoadSelectedOrderDetailsAsync");
            Check(vm.SelectedOrderDetails?.Id == 2, "Second order not loaded");
            gate.SetResult();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(vm.SelectedOrderDetails?.Id == 2, "First order overwrote second");
        });

        await Test("409 from atomic order update leaves editor open and displays conflict", async () =>
        {
            using Fake server = new(_ => Json("{\"message\":\"Order has changed\"}", HttpStatusCode.Conflict));
            var vm = ViewModel(server.Client);
            vm.Products.Add(new() { Id = 1, Quantity = 100, IsActive = true });
            vm.Products.Add(new() { Id = 2, Quantity = 100, IsActive = true });
            Set(vm, "SelectedOrderDetails", new OrderDetail { Id = 1, Version = new string('a',64), Status = "NewOrder", Items = [new() { Id = 10, ProductId = 1, Quantity = 2, UnitPrice = 20 }] });
            vm.EditableOrderItems.Add(new() { Id = 10, ProductId = 1, Quantity = 3, UnitPrice = 20 });
            vm.EditableOrderItems.Add(new() { ProductId = 2, Quantity = 1 });
            vm.OrderComment = "Changed comment";
            Check(!await vm.SaveOrderEditorAsync() && server.Methods.SequenceEqual(new[] { "PUT" }) && vm.OrderItemEditorError!.Contains("Order has changed"), "Conflict was ignored or draft split into requests");
        });

        await Test("401 clears authenticated state and report data", async () =>
        {
            bool fail = false;
            using Fake server = new(r => fail ? Json("{}", HttpStatusCode.Unauthorized) : Reports(r.RequestUri!.PathAndQuery));
            var vm = ViewModel(server.Client);
            await Call<bool>(vm, "LoadReportsAsync");
            fail = true;
            try { await server.Client.GetOrdersAsync(); } catch (ApiException) { }
            Check(!vm.IsAuthenticated && !vm.HasLoadedReports && vm.CurrentRoleName is null && vm.LoginError is not null, "Session data retained");
        });

        await Test("server without an order revision cannot fall back to unsafe editing", async () =>
        {
            using Fake server = new(_ => Json("{}"));
            var vm = ViewModel(server.Client);
            vm.Products.Add(new() { Id = 1, Quantity = 100, IsActive = true });
            Set(vm, "SelectedOrderDetails", new OrderDetail { Id = 1, Status = "NewOrder", Items = [new() { Id = 10, ProductId = 1, Quantity = 2 }] });
            vm.EditableOrderItems.Add(new() { Id = 10, ProductId = 1, Quantity = 3 });
            Check(!await vm.SaveOrderEditorAsync() && server.Paths.Count == 0, "Unsafe quantity edit sent");
            vm.EditableOrderItems.Clear();
            vm.EditableOrderItems.Add(new() { ProductId = 1, Quantity = 2 });
            Check(!await vm.SaveOrderEditorAsync() && server.Paths.Count == 0, "Multiple mutations sent");
        });

        await Test("late order list cannot repopulate after signout", async () =>
        {
            TaskCompletionSource gate = new();
            using Fake server = new(async (r, _) => { await gate.Task; return r.RequestUri!.AbsolutePath == "/api/orders" ? Json(Orders) : Json(Products); });
            var vm = ViewModel(server.Client, "Manager");
            Task pending = Call(vm, "LoadClientDataAsync");
            vm.SignOutCommand.Execute(null);
            gate.SetResult();
            await pending;
            Check(!vm.IsAuthenticated && vm.Orders.Count == 0 && vm.Products.Count == 0, "Late data crossed session boundary");
        });

        await Test("all four PDF variants generate in Ukrainian and English", () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "wpf-report-tests-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            try
            {
                foreach (string language in new[] { "uk", "en" })
                {
                    LocalizationManager.Instance.SetLanguage(language);
                    for (int tab = 0; tab < 4; tab++)
                    {
                        string file = Path.Combine(folder, $"{language}-{tab}.pdf");
                        PdfReportExporter.Save(file, tab, new() { TotalOrders = 1, TotalAmount = 40 }, new(2026, 10, 1), new(2026, 10, 25), new(2026, 10, 25), 1, 40,
                            [new() { OrderNumber = "O-1", CustomerFullName = "Покупець / Customer", StatusCode = "NewOrder", TotalAmount = 40 }],
                            [new() { ProductName = "Товар / Product", CategoryName = "Category", Quantity = 2, TotalAmount = 40 }], [], "Звіт / Report");
                        Check(File.ReadAllText(file).StartsWith("%PDF-"), "PDF missing");
                    }
                }
            }
            finally { LocalizationManager.Instance.SetLanguage("uk"); Directory.Delete(folder, true); }
            return Task.CompletedTask;
        });

        await Test("updated WPF views render with application resources", async () =>
        {
            using Fake server = new(r => Reports(r.RequestUri!.PathAndQuery));
            var vm = ViewModel(server.Client);
            string folder = Path.Combine(AppContext.BaseDirectory, "qa");
            Directory.CreateDirectory(folder);
            Render(new LoginView { DataContext = vm }, Path.Combine(folder, "login.png"), 1000, 800);
            Render(new UsersView { DataContext = vm }, Path.Combine(folder, "users.png"), 1000, 500);
            Render(new ReportsView { DataContext = vm }, Path.Combine(folder, "reports-empty.png"), 1280, 720);
            await Call<bool>(vm, "LoadReportsAsync");
            Render(new ReportsView { DataContext = vm }, Path.Combine(folder, "reports-loaded.png"), 1280, 720);
            var editor = new OrderEditorWindow { DataContext = vm };
            Render((FrameworkElement)editor.Content, Path.Combine(folder, "order-editor.png"), 1000, 650);
        });
    }

    private static void Render(FrameworkElement element, string path, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private sealed class Fake : IDisposable
    {
        public List<string> Paths { get; } = [];
        public List<string> Methods { get; } = [];
        public List<string?> Bodies { get; } = [];
        public List<string?> Authorization { get; } = [];
        public HttpClient Http { get; }
        public HttpOrdersApiClient Client { get; }
        public Fake(Func<HttpRequestMessage, HttpResponseMessage> respond) : this((r, _) => Task.FromResult(respond(r))) { }
        public Fake(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            Http = new(new Handler(async (r, ct) =>
            {
                Paths.Add(r.RequestUri!.PathAndQuery);
                Methods.Add(r.Method.Method);
                Bodies.Add(r.Content is null ? null : await r.Content.ReadAsStringAsync(ct));
                Authorization.Add(r.Headers.Authorization?.ToString());
                return await respond(r, ct);
            })) { BaseAddress = new("https://test.invalid/") };
            Http.DefaultRequestHeaders.Authorization = new("Bearer", "fixture-token");
            Client = new(Http);
        }
        public void Dispose() { Client.SignOut(); Http.Dispose(); }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
