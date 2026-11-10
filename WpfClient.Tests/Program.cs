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
using System.Windows.Markup;
using System.Xml.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using Orders.Reports;
using Orders.Workflow;
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
        // Load the real styles without App.StartupUri launching a login window during tests.
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "App.resources.xaml"));
        foreach (var element in source.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:WpfClient.Converters"))
            element.Name = XName.Get(element.Name.LocalName, "clr-namespace:WpfClient.Converters;assembly=WpfClient");
        var resources = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XAttribute(XNamespace.Xmlns + "converters", "clr-namespace:WpfClient.Converters;assembly=WpfClient"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        _ = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
            Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString())
        };
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

        await Test("parallel requests share a single refresh and use the rotated pair", async () =>
        {
            var refreshStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int refreshes = 0;
            using Fake server = new(async (r, _) =>
            {
                switch (r.RequestUri!.AbsolutePath)
                {
                    case "/api/auth/login":
                        return Json("""{"accessToken":"original","accessTokenExpiresAt":"2099-10-25T12:00:00Z","refreshToken":"first-refresh","refreshTokenExpiresAt":"2099-11-08T12:00:00Z"}""");
                    case "/api/auth/me":
                        return Json("""{"id":42,"login":"custom","fullName":"Actual user","role":"Director"}""");
                    case "/api/auth/refresh":
                        refreshes++;
                        Check((await r.Content!.ReadAsStringAsync()).Contains("first-refresh"), "Wrong refresh token submitted");
                        refreshStarted.TrySetResult(true);
                        await releaseRefresh.Task;
                        return Json("""{"accessToken":"rotated","accessTokenExpiresAt":"2099-10-26T12:00:00Z","refreshToken":"second-refresh","refreshTokenExpiresAt":"2099-11-09T12:00:00Z"}""");
                    default:
                        return r.Headers.Authorization?.Parameter == "rotated" ? Json("[]") : new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }
            });
            await server.Client.SignInAsync("custom", "password");
            var first = server.Client.GetOrdersAsync();
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = server.Client.GetOrdersAsync();
            releaseRefresh.SetResult(true);
            await Task.WhenAll(first, second);
            Check(refreshes == 1 && server.Authorization.Count(a => a == "Bearer rotated") == 2,
                "Concurrent requests repeated rotation or did not use the new access token");
        });

        await Test("a refresh finishing after sign-out cannot restore the old session", async () =>
        {
            var refreshStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRefresh = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int logins = 0;
            using Fake server = new(async (r, _) =>
            {
                switch (r.RequestUri!.AbsolutePath)
                {
                    case "/api/auth/login":
                        return ++logins == 1
                            ? Json("""{"accessToken":"old","accessTokenExpiresAt":"2099-10-25T12:00:00Z","refreshToken":"old-refresh","refreshTokenExpiresAt":"2099-11-08T12:00:00Z"}""")
                            : Json("""{"accessToken":"new-session","accessTokenExpiresAt":"2099-10-25T12:00:00Z"}""");
                    case "/api/auth/me":
                        return Json("""{"id":42,"login":"custom","fullName":"Actual user","role":"Director"}""");
                    case "/api/auth/refresh":
                        refreshStarted.TrySetResult(true);
                        await releaseRefresh.Task;
                        return Json("""{"accessToken":"late","accessTokenExpiresAt":"2099-10-26T12:00:00Z","refreshToken":"late-refresh","refreshTokenExpiresAt":"2099-11-09T12:00:00Z"}""");
                    default:
                        return r.Headers.Authorization?.Parameter == "new-session" ? Json("[]") : new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }
            });

            await server.Client.SignInAsync("custom", "password");
            var oldRequest = server.Client.GetOrdersAsync();
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            server.Client.SignOut();
            await server.Client.SignInAsync("custom", "password");
            releaseRefresh.SetResult(true);
            try { await oldRequest; throw new Exception("Old request unexpectedly succeeded"); }
            catch (OperationCanceledException) { }
            await server.Client.GetOrdersAsync();
            Check(server.Authorization.Last() == "Bearer new-session" && !server.Authorization.Contains("Bearer late"),
                "Late refresh overwrote the new session");
        });

        await Test("Telegram linking posts to the API and rejects links outside HTTPS t.me", async () =>
        {
            using Fake valid = new(_ => Json("""{"url":"https://t.me/OrdersTestBot?start=example"}"""));
            var url = await valid.Client.CreateTelegramLinkAsync();
            Check(url.Host == "t.me" && valid.Paths.Single() == "/api/telegram/link" &&
                valid.Methods.Single() == "POST", "Wrong link or endpoint");

            using Fake invalid = new(_ => Json("""{"url":"https://t.me.untrusted.example/OrdersTestBot"}"""));
            try { await invalid.Client.CreateTelegramLinkAsync(); }
            catch (InvalidOperationException) { return; }
            throw new Exception("Unsafe link accepted");
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
                Check(server.Paths[0].Contains("from=2026-10-01") && server.Paths[1].Contains("date=2026-10-25") &&
                    server.Paths.All(path => path.Contains("kyiv=true")), "Calendar changed API dates or diverged from the PDF period");
            }
            finally { CultureInfo.CurrentCulture = culture; }
        });

        await Test("failed report refresh clears old statistics", async () =>
        {
            bool fail = false;
            using Fake server = new(r => fail && r.RequestUri!.AbsolutePath.EndsWith("products")
                ? Json("{}", HttpStatusCode.InternalServerError) : Reports(r.RequestUri!.PathAndQuery));
            var vm = ViewModel(server.Client);
            Check(await Call<bool>(vm, "LoadReportsAsync") && vm.HasLoadedReports, "Initial report failed");
            fail = true;
            Check(!await Call<bool>(vm, "LoadReportsAsync") && !vm.HasLoadedReports && vm.ProductSalesReport.Count == 0, "Old report retained");
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

        await Test("navigation and cached report data follow refreshed API permissions", async () =>
        {
            bool revoked = false;
            using Fake server = new(r =>
            {
                string path = r.RequestUri!.AbsolutePath;
                return path switch
                {
                    "/api/auth/login" => Json("""{"accessToken":"access","accessTokenExpiresAt":"2099-10-25T12:00:00Z"}"""),
                    "/api/auth/me" => Json("""{"id":42,"login":"custom","fullName":"Observer","role":"Custom","permissions":["orders.read","reports.read","products.read"]}"""),
                    "/api/auth/me/permissions" => revoked
                        ? Json("""{"role":"Custom","revision":2,"permissions":["products.read"]}""")
                        : Json("""{"role":"Custom","revision":1,"permissions":["orders.read","reports.read","products.read"]}"""),
                    "/api/workflow" => Json("""{"revision":1,"states":[]}"""),
                    "/api/orders" => Json(Orders),
                    "/api/orders/1" => Json(Detail(1)),
                    "/api/orders/1/transitions" => Json("""{"workflowRevision":1,"orderVersion":"","actions":[]}"""),
                    "/api/products" => Json(Products),
                    "/api/reports/orders" when revoked => Json("{}", HttpStatusCode.Forbidden),
                    _ => Reports(path)
                };
            });
            var vm = new MainViewModel(new ApiClientSettings { UseMockData = false }, server.Client) { LoginName = "custom" };
            Check(await vm.SignInAsync("password"), "Could not sign in with a custom role");
            Check(vm.NavigationItems.Select(n => n.Key).SequenceEqual(new[] { "Orders", "Products", "Reports" }) &&
                vm.Orders.Count > 0 && vm.HasLoadedReports && !vm.CanManageOrders && !vm.CanManageProducts,
                "Visibility or write controls ignored the server snapshot");

            vm.NavigateCommand.Execute(vm.NavigationItems.Single(n => n.Key == "Reports"));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(vm.SelectedItem.Key == "Reports", "Reports view did not open");
            revoked = true;
            try { await server.Client.GetOrdersReportAsync(); }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) { }
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(vm.NavigationItems.Select(n => n.Key).SequenceEqual(new[] { "Products" }) &&
                vm.SelectedItem.Key == "Products" && vm.Orders.Count == 0 && vm.SelectedOrderDetails is null &&
                !vm.HasLoadedReports && !vm.CanViewReports, "Revoked data or navigation remained visible");
        });

        await Test("role editor sends checked sections and uses the returned revision", async () =>
        {
            const string permissions = "/api/roles/7/permissions";
            using Fake server = new(r => r.RequestUri!.AbsolutePath switch
            {
                "/api/roles" => Json("""[{"id":7,"code":"Observer","name":"Observer","isActive":true,"isSystem":false,"version":3}]"""),
                "/api/permissions" => Json("""[{"code":"orders.read"},{"code":"board.read","requires":"orders.read"},{"code":"reports.read"}]"""),
                "/api/roles/permissions" => Json("""[{"roleId":7,"revision":3,"permissions":["orders.read"]}]"""),
                permissions => Json("""{"roleId":7,"revision":4,"permissions":["orders.read","reports.read"]}"""),
                _ => Json("{}", HttpStatusCode.NotFound)
            });
            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var editor = new RoleAccessViewModel(server.Client, () => { changed.TrySetResult(true); return Task.CompletedTask; });
            await editor.LoadAsync();
            Check(editor.SelectedRole?.Version == 3 && editor.Choices.Single(c => c.Code == "orders.read").Enabled, "Matrix was not loaded");
            editor.Choices.Single(c => c.Code == "reports.read").Enabled = true;
            editor.SaveVisibilityCommand.Execute(null);
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var body = server.Bodies.Last()!;
            Check(server.Paths.Last() == permissions && server.Methods.Last() == "PUT" &&
                body.Contains("\"expectedRevision\":3") && body.Contains("\"reports.read\"") &&
                editor.SelectedRole?.Version == 4, "Wrong save payload or stale revision");
        });

        await Test("published workflow drives WPF columns, filters and transition requests", async () =>
        {
            using Fake server = new(r =>
            {
                string path = r.RequestUri!.AbsolutePath;
                return path switch
                {
                    "/api/auth/login" => Json("""{"accessToken":"access","accessTokenExpiresAt":"2099-10-25T12:00:00Z"}"""),
                    "/api/auth/me" => Json("""{"id":42,"login":"director","fullName":"Director","role":"Director","permissions":["orders.read","orders.edit","board.read","workflow.manage"]}"""),
                    "/api/auth/me/permissions" => Json("""{"role":"Director","revision":1,"permissions":["orders.read","orders.edit","board.read","workflow.manage"]}"""),
                    "/api/workflow" => Json("""{"revision":8,"states":[{"code":"Review","name":"На перевірці","sortOrder":1,"color":"#536BC5"}]}"""),
                    "/api/orders" => Json("""[{"id":1,"orderNumber":"O-1","statusCode":"Review","statusName":"На перевірці"}]"""),
                    "/api/orders/1" => Json("""{"id":1,"version":"order-version","statusCode":"Review","statusName":"На перевірці","canEditItems":true}"""),
                    "/api/orders/1/transitions" => Json("""{"workflowRevision":8,"orderVersion":"order-version","actions":[{"code":"approve","name":"Погодити","targetStatus":"Done","requiresComment":true}]}"""),
                    "/api/orders/1/transitions/approve" => Json("{}"),
                    _ => Json("{}", HttpStatusCode.NotFound)
                };
            });
            var vm = new MainViewModel(new ApiClientSettings { UseMockData = false }, server.Client) { LoginName = "director" };
            Check(await vm.SignInAsync("password"), "Workflow sign-in failed");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(vm.NavigationItems.Any(item => item.Key == "Workflow") &&
                vm.KanbanColumns.Single().Title == "На перевірці" && vm.KanbanColumns.Single().Orders.Count == 1 &&
                vm.OrderStatusFilterOptions.Any(option => option.Code == "Review") && vm.IsSelectedOrderEditable,
                "Workflow configuration did not reach WPF");
            var action = vm.AvailableActions.Single();
            vm.ChangeOrderStatusCommand.Execute(action);
            Check(!server.Paths.Contains("/api/orders/1/transitions/approve"), "Comment was not required");
            vm.TransitionComment = "Checked";
            vm.ChangeOrderStatusCommand.Execute(action);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            string? body = server.Bodies[server.Paths.IndexOf("/api/orders/1/transitions/approve")];
            Check(body is not null && body.Contains("\"expectedWorkflowRevision\":8") &&
                body.Contains("\"expectedVersion\":\"order-version\"") && body.Contains("\"comment\":\"Checked\""),
                "Transition request did not use visible order/workflow versions and the comment");
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

        await Test("Kanban sends one versioned move and reconciles conflicts and uncertain replies", async () =>
        {
            const string actionPath = "/api/orders/1/transitions";
            const string detailPath = "/api/orders/1";
            string state = "NewOrder";
            string version = "v1";
            long workflowRevision = 12;
            bool detailsUnavailable = false;
            int posts = 0;
            var releasePost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var initialActions = new WorkflowAction[]
            {
                new("review", "На перевірку", "Review", true),
                new("quick", "Швидка перевірка", "Review", false),
                new("invalid", "Same column", "NewOrder", false)
            };
            string OrderJson() => $$"""{"id":1,"orderNumber":"O-1","version":"{{version}}","statusCode":"{{state}}","statusCategory":"InProgress","totalAmount":40,"statusHistory":[]}""";
            using Fake server = new(async (request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                switch (path)
                {
                    case "/api/auth/login":
                        return Json("""{"accessToken":"access","accessTokenExpiresAt":"2099-10-25T12:00:00Z"}""");
                    case "/api/auth/me":
                        return Json("""{"id":42,"login":"operator","fullName":"Operator","role":"Custom","permissions":["orders.read","board.read"]}""");
                    case "/api/auth/me/permissions":
                        return Json("""{"role":"Custom","revision":1,"permissions":["orders.read","board.read"]}""");
                    case "/api/workflow":
                        return Json($$"""{"revision":{{workflowRevision}},"states":[{"code":"NewOrder","name":"New order"},{"code":"Review","name":"На перевірці"},{"code":"Done","name":"Готово"}]}""");
                    case "/api/orders":
                        return Json("[" + OrderJson() + "]");
                    case detailPath:
                        return detailsUnavailable ? Json("{}", HttpStatusCode.ServiceUnavailable) : Json(OrderJson());
                    case actionPath:
                        var actions = state switch
                        {
                            "NewOrder" => initialActions,
                            "Review" => [new WorkflowAction("finish", "Завершити", "Done", false)],
                            _ => Array.Empty<WorkflowAction>()
                        };
                        return Json(System.Text.Json.JsonSerializer.Serialize(new OrderWorkflowActions(workflowRevision, version, actions, state)));
                    case actionPath + "/review":
                        posts++;
                        await releasePost.Task;
                        state = "Review";
                        version = "v2";
                        return Json(OrderJson());
                    case actionPath + "/finish":
                        posts++;
                        if (posts == 2)
                        {
                            version = "v3";
                            return Json("{}", HttpStatusCode.Conflict);
                        }
                        state = "Done";
                        version = "v4";
                        detailsUnavailable = true;
                        throw new TaskCanceledException("Simulated request timeout");
                    default:
                        return Json("{}", HttpStatusCode.NotFound);
                }
            });
            var vm = new MainViewModel(new ApiClientSettings { UseMockData = false }, server.Client) { LoginName = "operator" };
            Check(await vm.SignInAsync("password"), "Board sign-in failed");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            OrderListItem Card(string status) => vm.KanbanColumns.Single(c => c.Status == status).Orders.Single();
            var move = await vm.PrepareKanbanMoveAsync(Card("NewOrder")) ?? throw new Exception("Could not prepare a move");
            Check(move.Actions.Count == 2 && vm.CanDropKanbanMove(move, "Review") && !vm.CanDropKanbanMove(move, "NewOrder") &&
                !vm.CanDropKanbanMove(move, "Done") && !vm.CanManageOrders, "Available targets or custom role rights are incorrect");
            var review = move.Actions.Single(a => a.Code == "review");
            var pending = vm.ExecuteKanbanMoveAsync(move, "Review", review, "Please check");
            await vm.ExecuteKanbanMoveAsync(move, "Review", review, "Duplicate");
            Check(posts == 1 && Card("NewOrder").Id == 1, "The card moved before confirmation or the POST was duplicated");
            releasePost.SetResult();
            await pending;
            Check(Card("Review").Id == 1 && vm.KanbanColumns.Single(c => c.Status == "Review").Total == 40 &&
                server.Bodies[server.Paths.IndexOf(actionPath + "/review")]!.Contains("\"expectedVersion\":\"v1\"") &&
                server.Bodies[server.Paths.IndexOf(actionPath + "/review")]!.Contains("\"expectedWorkflowRevision\":12"),
                "Confirmation or the version from before dragging was lost");

            workflowRevision++;
            Check(await vm.PrepareKanbanMoveAsync(Card("Review")) is null && posts == 1, "Drag silently used a workflow revision not shown on the board");
            var conflicting = await vm.PrepareKanbanMoveAsync(Card("Review")) ?? throw new Exception("Move missing");
            await vm.ExecuteKanbanMoveAsync(conflicting, "Done", conflicting.Actions.Single(), null);
            Check(posts == 2 && Card("Review").Id == 1 && vm.KanbanMoveMessage!.StartsWith(LocalizationManager.Instance["KanbanConflict"]) &&
                server.Paths.LastIndexOf(detailPath) > server.Paths.LastIndexOf(actionPath + "/finish"), "Conflict was not reconciled");

            var uncertain = await vm.PrepareKanbanMoveAsync(Card("Review")) ?? throw new Exception("Move missing after conflict");
            Check(uncertain.OrderVersion == "v3", "A subsequent drag did not get a new version");
            await vm.ExecuteKanbanMoveAsync(uncertain, "Done", uncertain.Actions.Single(), null);
            Check(posts == 3 && Card("Review").Id == 1, "An unconfirmed move changed the board");
            detailsUnavailable = false;
            int beforeRead = server.Paths.Count;
            Check(await vm.PrepareKanbanMoveAsync(Card("Review")) is null && Card("Done").Id == 1 && posts == 3 &&
                server.Paths[beforeRead] == detailPath, "A failed verification did not require a fresh detail/history read");
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

        await Test("WPF downloads only validated server PDFs and uses the selected tab and date", async () =>
        {
            bool valid = false;
            using Fake server = new(_ => valid ? PdfFixture() : Json("""{"error":"not PDF"}"""));
            var vm = ViewModel(server.Client);
            vm.SelectedReportTabIndex = 1;
            await Call(vm, "ExportReportsToPdfAsync");
            Check(server.Paths.Single() == "/api/reports/pdf" && server.Methods.Single() == "POST" &&
                server.Bodies.Single()!.Contains("\"preset\":\"orders\"") &&
                server.Bodies.Single()!.Contains("\"from\":\"2026-10-25\"") &&
                vm.ReportPdfMessage?.Contains("did not return a PDF") == true, "Export used a local or wrong report");
            valid = true;
            var pdf = await server.Client.GenerateReportPdfAsync(new GenerateReportPdf(Preset: "summary"));
            Check(pdf.FileName == "fixture.pdf" && pdf.Content.AsSpan().StartsWith("%PDF-"u8), "PDF bytes or filename were lost");
        });

        await Test("report builder persists reordered columns and previews a draft through the API", async () =>
        {
            var source = new ReportSource(new("orders", "Замовлення", "Orders"),
                [new("number", "Номер", "Number"), new("status", "Стан", "State"), new("amount", "Сума", "Amount")],
                ["status"], new("orders", ["number", "status", "amount"], "amount", Descending: true));
            var saved = new ReportTemplate(7, "Звіт за станом", 42, 2, source.DefaultLayout with { Columns = ["status", "number", "amount"] });
            using Fake server = new(request => request.RequestUri!.AbsolutePath switch
            {
                "/api/report-templates/catalog" => Json(System.Text.Json.JsonSerializer.Serialize(new[] { source })),
                "/api/report-templates" when request.Method == HttpMethod.Post => Json(System.Text.Json.JsonSerializer.Serialize(saved)),
                "/api/report-templates" => Json("[]"),
                "/api/reports/pdf/preview" or "/api/reports/pdf" => PdfFixture(),
                _ => Json("{}", HttpStatusCode.NotFound)
            });
            var deliveries = new List<ReportPdfAction>();
            var editor = new ReportTemplateViewModel(server.Client, true, new(2026, 10, 1), new(2026, 10, 25),
                () => true, (file, action) => { deliveries.Add(action); return Task.FromResult(file.FileName == "fixture.pdf"); }, () => true);
            await editor.LoadAsync([new("NewOrder", "Нове"), new("Registered", "Зареєстроване")]);
            editor.Name = saved.Name;
            editor.MoveColumnCommand.Execute("down");
            editor.SaveCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(editor.SelectedTemplate?.Version == 2 && server.Bodies.Last()!.Contains("[\"status\",\"number\",\"amount\"]"),
                "Builder lost the chosen column order");
            editor.PreviewCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(server.Paths.Last() == "/api/reports/pdf/preview" && server.Bodies.Last()!.Contains("\"draft\":{") &&
                deliveries.SequenceEqual(new[] { ReportPdfAction.Preview }), "Draft did not use the sample preview endpoint");
            editor.DownloadCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(server.Paths.Last() == "/api/reports/pdf" && server.Bodies.Last()!.Contains("\"templateId\":7") &&
                server.Bodies.Last()!.Contains("\"expectedVersion\":2") && deliveries.SequenceEqual(new[] { ReportPdfAction.Preview, ReportPdfAction.Save }),
                "Download did not use the saved template version");
            editor.PrintCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(server.Paths.Last() == "/api/reports/pdf" && server.Bodies.Last()!.Contains("\"draft\":{") &&
                deliveries.Last() == ReportPdfAction.Print, "Print did not use the current layout with live data");

            string folder = Path.Combine(AppContext.BaseDirectory, "qa");
            Directory.CreateDirectory(folder);
            var window = new ReportTemplateWindow(server.Client, true, new(2026, 10, 1), new(2026, 10, 25), [], () => true)
            { DataContext = editor };
            Render((FrameworkElement)window.Content, Path.Combine(folder, "report-builder.png"), 1000, 730);
        });

        await Test("PDF window renders pages and the order print action requests the selected order", async () =>
        {
            byte[] content = PreviewPdfFixture();
            var preview = new ReportPreviewWindow(content);
            await Call(preview, "LoadAsync");
            var image = (Image)preview.FindName("PageImage");
            var next = (Button)preview.FindName("NextPage");
            Check(image.Source is BitmapImage { PixelWidth: > 0 } && next.IsEnabled, "Windows PDF did not render the first page");
            await (Task)typeof(ReportPreviewWindow).GetMethod("RenderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(preview, [1])!;
            Check(!next.IsEnabled && ((Button)preview.FindName("PreviousPage")).IsEnabled, "Page navigation did not advance");
            preview.Close();

            var assembly = typeof(HttpOrdersApiClient).Assembly;
            var open = (Task)assembly.GetType("WpfClient.Services.PdfPageReader")!.GetMethod("OpenAsync")!.Invoke(null, [content])!;
            await open;
            using var reader = (IDisposable)open.GetType().GetProperty("Result")!.GetValue(open)!;
            var paginatorType = assembly.GetType("WpfClient.Services.ReportPdfPrinter+PdfPaginator")!;
            var paginator = (DocumentPaginator)Activator.CreateInstance(paginatorType,
                reader, new Size(500, 700), new Rect(20, 30, 450, 630), 1, 1)!;
            using var page = paginator.GetPage(0);
            Check(paginator.PageCount == 1 && page.Visual is DrawingVisual && page.ContentBox.X >= 20 && page.ContentBox.Bottom <= 660,
                "Print pagination did not fit the selected page into the printable area");

            using Fake server = new(_ => Json("{}"));
            var vm = ViewModel(server.Client, "Viewer");
            Set(vm, "SelectedOrderDetails", new OrderDetail { Id = 14 });
            await Call(vm, "PrintSelectedOrderAsync");
            Check(server.Paths.Single().StartsWith("/api/orders/14/pdf?language=") && server.Methods.Single() == "GET" &&
                vm.ApiErrorMessage?.Contains("did not return a PDF") == true, "Print did not request the selected order");

            if (Environment.GetEnvironmentVariable("CODEX_REPORT_PREVIEW_QA_PATH") is { Length: > 0 } sample)
            {
                var realPreview = new ReportPreviewWindow(await File.ReadAllBytesAsync(sample));
                await Call(realPreview, "LoadAsync");
                Check(((Image)realPreview.FindName("PageImage")).Source is BitmapImage, "Server-generated sample did not render in WPF");
                string folder = Path.Combine(AppContext.BaseDirectory, "qa");
                Directory.CreateDirectory(folder);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Render((FrameworkElement)realPreview.Content, Path.Combine(folder, "report-preview.png"), 970, 760);
                realPreview.Close();
            }
        });

        await Test("updated WPF views render with application resources", async () =>
        {
            using Fake server = new(r => Reports(r.RequestUri!.PathAndQuery));
            var vm = ViewModel(server.Client);
            string folder = Path.Combine(AppContext.BaseDirectory, "qa");
            Directory.CreateDirectory(folder);
            Render(new LoginView { DataContext = vm }, Path.Combine(folder, "login.png"), 1000, 800);
            Render(new UsersView { DataContext = vm }, Path.Combine(folder, "users.png"), 1000, 500);
            Render(new RoleAccessView { DataContext = new RoleAccessViewModel(server.Client, () => Task.CompletedTask) }, Path.Combine(folder, "roles.png"), 1000, 700);
            Render(new ReportsView { DataContext = vm }, Path.Combine(folder, "reports-empty.png"), 1280, 720);
            await Call<bool>(vm, "LoadReportsAsync");
            Render(new ReportsView { DataContext = vm }, Path.Combine(folder, "reports-loaded.png"), 1280, 720);
            var editor = new OrderEditorWindow { DataContext = vm };
            Render((FrameworkElement)editor.Content, Path.Combine(folder, "order-editor.png"), 1000, 650);
        });

        await Test("workflow editor renders tabs and saves, validates and publishes the draft", async () =>
        {
            var draft = new WorkflowDraft(1, 1, new(
                [
                    new("NewOrder", "Нове замовлення", 1, "New", true, false, true),
                    new("Review", "На перевірці", 2, "InProgress", false, false, false, "#536BC5"),
                    new("Done", "Завершене", 3, "Completed", false, true, false)
                ],
                [
                    new("review", "Передати на перевірку", "NewOrder", "Review", ["Director"], ["CommentRequired"]),
                    new("close", "Завершити", "Review", "Done", [], [])
                ]));
            using Fake server = new(r => r.RequestUri!.AbsolutePath switch
            {
                "/api/workflow/draft" when r.Method == HttpMethod.Put =>
                    Json(System.Text.Json.JsonSerializer.Serialize(draft with { Version = 2 })),
                "/api/workflow/draft" => Json(System.Text.Json.JsonSerializer.Serialize(draft)),
                "/api/workflow/roles" => Json("""[{"code":"Admin","name":"Адміністратор"},{"code":"Director","name":"Директор"}]"""),
                "/api/workflow/rule-types" => Json("""[{"code":"CommentRequired","name":"Comment is required"},{"code":"HasItems","name":"Has items"}]"""),
                "/api/workflow/draft/validate" => Json("""{"errors":[],"affectedOrders":12}"""),
                "/api/workflow/draft/publish" =>
                    Json(System.Text.Json.JsonSerializer.Serialize(draft with { Version = 3, PublishedRevision = 2 })),
                _ => Json("{}", HttpStatusCode.NotFound)
            });
            bool reloaded = false;
            var vm = new WorkflowEditorViewModel(server.Client, () => { reloaded = true; return Task.CompletedTask; });
            await vm.LoadAsync();
            Check(vm.IsReady && !vm.IsDirty && vm.SelectedTransition?.Rules.First().Enabled == true, "Workflow draft did not load");
            var view = new WorkflowEditorView { DataContext = vm };
            string folder = Path.Combine(AppContext.BaseDirectory, "qa");
            Directory.CreateDirectory(folder);
            Render(view, Path.Combine(folder, "workflow-states.png"), 960, 620);
            ((TabControl)view.FindName("WorkflowTabs")).SelectedIndex = 1;
            Render(view, Path.Combine(folder, "workflow-transitions.png"), 960, 620);
            vm.SelectedState!.Name = "New display name";
            Check(vm.IsDirty && vm.SaveCommand.CanExecute(null) && !vm.PublishCommand.CanExecute(null), "Unsaved changes were not tracked");
            vm.SaveCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(!vm.IsDirty && server.Bodies.Last()!.Contains("\"expectedDraftVersion\":1") &&
                server.Bodies.Last()!.Contains("New display name"), "Editor did not save the changed definition");
            vm.ValidateCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(vm.PublishCommand.CanExecute(null), "Validated draft could not be published");
            vm.PublishCommand.Execute(null);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(reloaded && server.Paths.Last() == "/api/workflow/draft/publish" &&
                server.Bodies.Last()!.Contains("\"expectedDraftVersion\":2"), "Publication used the wrong draft version");
        });

        await Test("Kanban renders an empty drop target and the action/comment dialog", () =>
        {
            using Fake server = new(_ => Json("[]"));
            var vm = ViewModel(server.Client);
            vm.KanbanColumns[0].Orders.Add(new()
            {
                Id = 1, Number = "ORD-0001", Customer = "Демо клієнт", CreatedAt = new(2026, 11, 9), Status = "NewOrder", Total = 40
            });
            vm.KanbanColumns[0].RefreshMetrics();
            vm.KanbanColumns[1].IsDropTarget = true;
            string folder = Path.Combine(AppContext.BaseDirectory, "qa");
            Directory.CreateDirectory(folder);
            Render(new KanbanView { DataContext = vm }, Path.Combine(folder, "kanban-drop-target.png"), 1160, 550);
            var dialog = new KanbanMoveWindow("На перевірці", [new("review", "На перевірку", "Review", true)]);
            Render((FrameworkElement)dialog.Content, Path.Combine(folder, "kanban-move-dialog.png"), 465, 325);
            var submit = (Button)dialog.FindName("ConfirmButton");
            var comment = (TextBox)dialog.FindName("CommentInput");
            Check(!submit.IsEnabled, "A required comment was not enforced in the dialog");
            comment.Text = "Verified";
            Check(submit.IsEnabled, "The dialog could not submit a valid transition");
            return Task.CompletedTask;
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

    private static HttpResponseMessage PdfFixture()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("%PDF-1.7"u8.ToArray()) };
        response.Content.Headers.ContentType = new("application/pdf");
        response.Content.Headers.ContentDisposition = new("attachment") { FileNameStar = "fixture.pdf" };
        return response;
    }

    private static byte[] PreviewPdfFixture()
    {
        const string drawing = "q 0.2 0.4 0.7 rg 40 60 120 40 re f Q";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 150] /Resources << >> /Contents 5 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 150] /Resources << >> /Contents 5 0 R >>",
            $"<< /Length {drawing.Length} >>\nstream\n{drawing}\nendstream"
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
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
