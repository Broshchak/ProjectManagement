namespace Application.Security
{
    /// <summary>Permission strings used by [HasPermission]</summary>
    public static class Permissions
    {
        public const string DashboardRead = "dashboard.read";
        public const string BoardRead = "board.read";
        public const string CustomersRead = "customers.read";
        public const string ShipmentsRead = "shipments.read";
        public const string PaymentsRead = "payments.read";
        public const string WorkflowManage = "workflow.manage";
        public const string RolesManageVisibility = "roles.visibility.manage";
        public const string OrdersRead = "orders.read";
        public const string OrdersCreate = "orders.create";
        public const string OrdersEdit = "orders.edit";
        public const string UsersRead = "users.read";
        public const string ProductsRead = "products.read";
        public const string ProductsManage = "products.manage";
        public const string UsersManage = "users.manage";
        public const string RolesManage = "roles.manage";
        public const string ReportsRead = "reports.read";
        public const string ReportsTelegramSend = "reports.telegram.send";
        /// <summary>A user may use tools for system diagnostics</summary>
        public const string SystemDiagnostics = "system.diagnostics";
        /// <summary>A user may link their own Telegram account</summary>
        public const string TelegramLink = "telegram.link";
        /// <summary>The bot's service account may act for linked Telegram users</summary>
        public const string BotAct = "bot.act";
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Viewer = "Viewer";
        public const string Director = "Director";
        public const string TelegramBot = "TelegramBot";

        private static readonly HashSet<string> SystemCodes =
            new([Admin, Manager, Viewer, Director, TelegramBot], StringComparer.OrdinalIgnoreCase);

        public static bool IsSystemCode(string code) => SystemCodes.Contains(code);
    }

    public static class AppClaims
    {
        public const string Subject = "sub";
        public const string Role = "role";
    }
}
