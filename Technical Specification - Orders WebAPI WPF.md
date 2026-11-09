# Technical Specification

## Project

**Name:** Orders Processing System  
**Type:** C# WebAPI application, thin WPF desktop client, Telegram bot  
**Project management methodology:** Agile Scrum  
**Planning scope:** 3 initial sprints and 3 extension sprints

**Requirements revision:** 25 October 2026. This specification describes target behaviour, not completed implementation.

## 1. Project Goal

The goal is to develop a software system for processing product orders stored in a server-side database, initially PostgreSQL, with a replaceable persistence implementation.

The system must support:

- creating and managing orders;
- administering the product catalog;
- controlling allowed order state transitions;
- blocking actions that violate business rules;
- role-based access for different users;
- access to system functionality through WebAPI;
- a thin WPF desktop client;
- collecting information and statistics through a Telegram bot;
- generating and sending a management report in Telegram by request.

## 2. Team and Scrum Roles

The project team consists of 3 participants. Because the team is small, Scrum roles and technical responsibilities may be combined.

| Participant | Main responsibility | Additional responsibility |
| --- | --- | --- |
| Participant 1 | Backend WebAPI, domain model, order state rules | Product Owner duties, requirement clarification, sprint result acceptance |
| Participant 2 | WPF client, WebAPI integration, database work, client-side validation | Scrum Master duties, Scrum process organization, Trello board control |
| Participant 3 | Telegram bot, statistics, reports, testing | QA, integration checks, documentation |

Development should run in parallel inside each sprint:

- Participant 1 works on backend API, domain logic, and access rules.
- Participant 2 works on the WPF client and database using agreed DTOs, mock data, or test endpoints when needed.
- Participant 3 works on the Telegram bot, testing, seed data, and documentation.
- Integration is performed at the end of each sprint or after completion of key tasks.
- If API endpoints are not ready yet, the WPF client may temporarily use mock services and later switch to the real backend.
- Sprint Review should demonstrate an integrated system increment, not only isolated modules.

## 3. Stakeholders

- Director or manager receiving statistics and reports;
- operator or manager creating and processing orders;
- development team;
- system administrator or person responsible for database and access configuration.

## 4. Scope

### Included in the project

- C# ASP.NET Core WebAPI, thin WPF client and Python Telegram bot;
- orders, customers, products, price snapshots, stock checks and status history;
- authentication and server-side checks of current user and role activity and permissions;
- role creation, blocking/unblocking, deletion and section visibility management by Admin only;
- one configurable workflow, server-side state machine, configuration APIs and WPF editor for Admin and Director only;
- drag-and-drop between board columns with transition, version and concurrency checks;
- report templates, a simple builder and server-generated PDF downloads for WPF and Telegram;
- test delivery integration with Nova Poshta / Nova Post and LiqPay sandbox payments;
- all database operations behind application interfaces; provider selection in server configuration;
- PostgreSQL accessed through application interfaces;
- clear API errors, logging and tests of rules, permissions, retries and concurrency.

### Outside this release

Production acquiring, automatic refunds, real shipments created for testing, arbitrary workflow scripts, full BPMN/Jira, multiple independent workflows, a page-layout report designer, arbitrary SQL/HTML/formulas, record/field-level permissions, a universal action-permission editor, complete stock reservation/accounting, a multi-level CRM and mobile apps are outside scope.

## 5. Main Entities

### Role

Contains an identifier, unique immutable code, editable name, is_active, is_system, record version and timestamps. System role codes are reserved; clients cannot set is_system.

### User

Contains an identifier, role reference, login, password hash or another authentication mechanism, full name, Telegram access flag, Telegram user id, active flag, creation date, and update date.

### Customer

Contains an identifier, full name, phone, email, address, creation date, and update date.

### OrderStatus

Contains an identifier, status code, status name, sorting order, and final-state flag.

### Order

Contains an identifier, order number, current status reference, customer reference, creator user reference, comment, creation date, and update date.

Order items, order total, and status change history are stored in related tables and views. Registration, approval, shipping, invoicing, and cancellation dates are determined from status history records.

### ProductCategory

Contains an identifier, name, description, active flag, creation date, and update date.

### Product

Contains an identifier, category reference, name, description, active flag, creation date, and update date.

The current product price is stored in `product_prices`. Product stock is stored in `product_stocks`; in the `v_products_current` view it is exposed as `available_quantity`.

### ProductPrice

Contains an identifier, product reference, price, currency code, and creation date.

### ProductStock

Contains an identifier, product reference, quantity, and update date. `product_stocks.quantity` is the source of truth for available product quantity.

### OrderItem

Contains an identifier, order reference, product reference, quantity, unit price at the moment of adding the product to the order, creation date, and update date.

Line total is calculated as `quantity * unit_price` in the `v_order_items_details` view. Existing order items must not change automatically when the catalog product price changes later.

### OrderStatusHistory

Contains an identifier, order reference, previous status, new status, user who changed the status, change date and time, and comment.

## 6. Database Structure

The initial database structure is described by these SQL scripts. Existing databases must be upgraded using non-destructive migrations; schema recreation containing DROP is for disposable test databases only:

- `SQL/01_create_schema.sql`
- `SQL/02_seed_data.sql`

Main tables:

| Table | Purpose |
| --- | --- |
| `roles` | Role catalog |
| `app_users` | User accounts |
| `order_statuses` | Order status catalog |
| `customers` | Customers |
| `product_categories` | Product categories |
| `products` | Product catalog |
| `product_prices` | Current product prices |
| `product_stocks` | Current product stock |
| `orders` | Orders |
| `order_items` | Order items with unit price snapshot |
| `order_status_history` | Order status change history |

Database views:

- `v_products_current` - current products with category, price, and stock;
- `v_order_items_details` - order item details with line total;
- `v_order_totals` - total amount per order.

Tables with `updated_at` use the `set_updated_at` trigger to update modification time before `UPDATE`.

Seed data must include:

- roles `Admin`, `Manager`, `Viewer`, `Director`;
- statuses `NewOrder`, `Registered`, `Granted`, `Shipped`, `Invoiced`, `Cancelled`;
- test users `admin`, `manager`, `viewer`, `director`;
- base categories, products, prices, and stock;
- demo customer, demo order `ORD-0001`, one order item, and initial status history.

### Extension entities and tables

| Entity / table | Required data |
| --- | --- |
| RefreshSession / refresh_sessions | User/session, token hash, expiry, rotation and revocation |
| TelegramLinkToken / telegram_link_tokens | User, token hash, expiry and one-time consumption |
| RolePermission / role_permissions | Role and known permission code, unique pair |
| Workflow / workflow_settings | Published revision |
| WorkflowDraft / workflow_drafts | Draft document, version, editor, timestamp |
| OrderTransition / order_transitions | Source/target states, action label, active flag |
| TransitionRole / order_transition_roles | Transition and permitted role |
| TransitionRule / order_transition_rules | Transition, known rule code, typed parameters, evaluation order |
| ReportTemplate / report_templates | Name, dataset, versioned JSON layout, author, activity, record version |
| OrderShipment / order_shipments | Order, provider reference, shipment number/status, Test mode, operation result and last check |
| OrderPayment / order_payments | Order, unique merchant/provider identifiers, amount, currency, status, Test mode and order version |

Extend statuses with initial/active flags, edit-items permission, semantic category and colour. Roles and workflow configuration use versions for concurrent updates. Historical records and financial facts must be preserved. Each supported provider has compatible migrations; PostgreSQL-specific triggers and locking remain infrastructure details.

## 7. User Roles and Access Rights

### System roles

| Role | Capabilities |
| --- | --- |
| Admin | Users, roles and visibility management; workflow editor; products, orders, statistics and reports |
| Director | Workflow editor; product/order management; viewing users, statistics and reports; no user, role or visibility administration |
| Manager / Operator | Creating/processing orders, viewing products and permitted transitions |
| Viewer | Read-only access to permitted sections |
| Custom role | Initially no permissions; Admin grants section visibility and the workflow editor can assign explicit transitions; no inherited system privileges |

Visibility does not grant edit permissions. Existing system-role action rights remain. Assigning a custom role a transition does not automatically grant order creation/editing or product management. A general editor for arbitrary action permissions is outside this release.

Workflow.Manage is permanently restricted to system Admin and Director. Roles.Manage, Roles.ManageVisibility and user administration are permanently restricted to Admin. Display names, visibility settings and transition configuration cannot grant these protected capabilities.

### Role lifecycle — Admin only

- Create a role with a unique immutable code and editable name. New roles are active with no permissions. System codes are reserved; systemness is not client-controlled.
- Rename, block or unblock using the expected record version. Blocked roles cannot be assigned to users.
- Blocking keeps users, assignments and history but denies login and protected requests for all users with that role, including requests using previously issued JWTs and Telegram access. Check current user/role activity and permissions in the database on every request.
- Unblocking permits login again; an unexpired JWT can pass current activity checks again. Permanent token revocation is a separate future capability.
- The Admin role cannot be blocked or deleted. Changes to users must never remove, deactivate or reassign the last active administrator; checks are atomic.
- Other system roles may be blocked but never deleted. Custom roles may be deleted only when no users (including inactive users), Telegram links, published transitions, saved workflow drafts or other records reference them. Otherwise return 409 with dependencies; never cascade-delete related data.
- Director cannot call administrative role APIs. Minimal role dictionaries needed by the workflow editor are exposed by its own authorised endpoints.

### Section visibility — Admin only

Configure visibility of dashboard, orders, board, products, customers, users, reports, shipments and payments. The permission catalog is fixed in code; assignments are stored in role_permissions. Missing or unknown permissions deny access. Board access requires both Board.View and Orders.View.

The client builds navigation from effective permissions. The server applies equivalent restrictions to endpoints, nested DTOs, PDF and Telegram; inaccessible data cannot be retrieved through another route. Following permission refresh or 403, the client closes the inaccessible section and clears its data. Concurrent role/visibility updates require expected versions and return 409 on conflict. Protected administrative capabilities cannot be removed through this matrix.

## 8. Order States

Admin and Director configure states through APIs and the WPF editor. One published workflow applies to all orders; drafts do not change live behaviour.

The initial template contains NewOrder, Registered, Granted, Shipped, Invoiced and Cancelled. Invoiced means an invoice was issued, not that payment was confirmed. Payments are stored separately; an optional Paid state requires PaymentSucceeded. Each state has an immutable code, label, display order, active/initial/final flags, semantic category, edit-items permission and colour.

The board, filters, available actions and status statistics use API configuration. Adding a state does not require rebuilding WPF. State labels are not proof of financial events.

Initial transitions: NewOrder → Registered/Cancelled; Registered → Granted/Cancelled; Granted → Shipped/Cancelled; Shipped → Invoiced. These are seed configuration, not the only possible transitions.

Each transition has an action label, allowed roles and AND-combined rules from a known catalog: HasItems, ProductsActive, StockAvailable, CustomerPhoneRequired, CommentRequired, followed by PaymentSucceeded and ShipmentCreated after integrations. New rule types require server code; existing parameters are configurable without deployment. No executable user scripts.

A single state-machine service checks the current user/role, order version, workflow revision, current state, transition and guards within a transaction; it saves the new state and exactly one history record. All status-change routes delegate to it. Arbitrary statusId assignment is forbidden.

Publishing validates exactly one initial state, unique codes, references, reachability, accidental dead ends and no outgoing transitions from final states. It displays impact on current orders and updates the published revision atomically. Referenced states/codes cannot be deleted or silently changed. Deactivating occupied states or removing exits requires relocating affected orders first. Publishing and transitions coordinate their revision checks to prevent races. A rejected operation leaves data unchanged; version conflicts return 409.

## 9. Business Rules

- Items can be edited only in a non-final state with canEditItems and the appropriate user action right; initially NewOrder and Registered.
- Products must be active, with positive price and quantity within available stock. Creation and editing share consistent validation.
- Existing item prices are historical snapshots and are not overwritten by catalog changes.
- Registration requires items; approval checks product availability and quantities. Access, integrity and finality invariants cannot be disabled by configured rules.
- Stock checking does not reserve stock; complete reservation/accounting is outside scope.
- Every successful transition writes exactly one history record. Blocked roles cannot execute previously assigned transitions.
- Shipment, payment and business status are separate. A shipment document is not proof of dispatch and an invoice is not proof of payment.
- An active payment attempt freezes order contents and amount. A late successful payment after cancellation is retained and flagged for manual handling.
- Failed operations return clear errors and do not leave partially saved changes.

## 10. WebAPI Requirements

### Authentication and client session

POST /api/auth/login accepts {login, password}; POST /api/auth/refresh accepts {refreshToken}. Both endpoints return the same JSON with two tokens and their individual expiration times:

```json
{
  "accessToken": "<access-token>",
  "accessTokenExpiresAt": "2026-10-25T10:15:00Z",
  "refreshToken": "<refresh-token>",
  "refreshTokenExpiresAt": "2026-11-08T10:00:00Z",
  "tokenType": "Bearer"
}
```

Use camelCase fields and UTC ISO 8601 timestamps including the time zone. Example dates are illustrative. Both expiration fields are required; clients use server-provided expiry instead of hard-coded TTLs. Every successful refresh rotates the refresh token and returns a new pair and both new expiration times. Store only the refresh-token hash, expiry and session state on the server. Retrieve the user profile through /api/auth/me.

Update WPF DTOs, IAuthApiClient, HttpOrdersApiClient and session state together with the backend:

- Keep both tokens and expiration times in memory. Do not retain the password. Send access as Bearer on business requests and refresh only to /api/auth/refresh.
- Renew access early, for example 30 seconds before expiry. Concurrent calls share one in-flight refresh; replace the token pair and expiry values atomically.
- Access expiry does not end a session with a valid refresh token. A 401 allows at most one refresh and one retry of a request rejected before execution. A 403 does not trigger refresh. Do not blindly replay writes after a timeout or unknown outcome.
- Expired, revoked or rejected refresh ends the session: clear both tokens, expiry values and user data, then show login. Network failures do not start retry loops. Never automatically reuse an old refresh token after an uncertain rotation outcome.
- Sign-out cancels requests and clears state. A late refresh response cannot restore a signed-out or replaced session. Refresh profile/permissions so WPF does not retain stale role information.
- Never log tokens or persist them in ordinary files/settings. The Python bot consumes the same response format under the service-session rules of BOT_INTEGRATION.md.

### Business APIs

Keep authentication, current user, order/item/product/customer/user operations and JSON statistics. Business validation runs on the server; WPF and Telegram use the API only.

| Area | Proposed endpoints |
| --- | --- |
| Roles — Admin only | GET/POST /api/roles; PUT /api/roles/{id}; PUT /api/roles/{id}/active; DELETE /api/roles/{id} |
| Permissions | GET /api/permissions; GET /api/roles/permissions; PUT /api/roles/{roleId}/permissions — Admin; GET /api/auth/me/permissions — current user |
| Workflow | GET /api/workflow; GET /api/workflow/rule-types; GET/PUT /api/workflow/draft; POST /api/workflow/draft/validate; POST /api/workflow/draft/publish |
| Transitions | GET /api/orders/{id}/transitions; POST /api/orders/{id}/transitions/{transitionId} |
| Reports | CRUD /api/report-templates; POST /api/reports/pdf |
| Delivery | GET /api/delivery/cities; GET /api/delivery/warehouses; GET/POST /api/orders/{id}/shipment; POST /api/orders/{id}/shipment/refresh |
| Payments | POST/GET /api/orders/{id}/payments; POST /api/payments/{id}/refresh; POST /api/payments/liqpay/callback |

Published workflow is readable by users with order access and workflow editors. Drafts, rule catalog, validation and publication are Admin/Director only. Save the complete draft in one PUT with expectedDraftVersion; publishing also checks expectedWorkflowRevision. Transitions require the visible order's expectedVersion, workflow revision and comment. Role updates require expectedVersion; DELETE uses If-Match. Minimal role dictionaries for workflow editors do not expose role-administration APIs to Director.

Use DTOs. Return 401 for missing authentication, 403 for denied access, 404 for missing objects, 409 for version/dependency conflicts and a consistent 400 or 422 for invalid data/rules.

### Persistence and architecture

Preserve WebApi, Application, Domain, Infrastructure, WpfClient, TelegramBot and tests. Use Controller → Application service → repository/query and transaction interfaces → Infrastructure → database. Extend existing IOrderRepository, IProductRepository, IUserRepository and IUnitOfWork; add small interfaces for missing operations, including roles/permissions, workflow, reports, shipments and payments.

Controllers and business services do not use DbContext, DbSet, IQueryable, SQL or Npgsql-specific exceptions. Infrastructure implements locking, shared transaction sessions, migrations and translation of provider errors to common conflicts. Reuse EF implementations where semantics match.

Database:Provider and connection settings select the implementation at API startup. Switching requires restart, does not copy data and does not change business code or client contracts. The supported database is PostgreSQL. Application interfaces isolate access to it.

Use one workflow service, a small rule evaluator, one PDF service and external provider adapters. Keep new WPF view models separate. Microservices, a broker and a universal BPMN engine are unnecessary for this scope.

### Server reports

Templates contain a dataset (orders, product sales, low stock, summary), title, known columns/order, filters, sorting, one grouping, totals, note and page orientation. Use known blocks, not arbitrary SQL/scripts/HTML. Admin/Director edit shared templates; users need Reports.View and permissions for all data/blocks used. Missing source permissions deny generation.

The server validates template schema/version and limits, builds one data snapshot and renders QuestPDF with bundled Ukrainian fonts. Return application/pdf and a file name. Preview also uses server generation; JSON reports remain for screens. Define sales semantics; financial totals use confirmed payments and mark test data separately. Convert Europe/Kyiv date boundaries to UTC including daylight-saving transitions. Synchronous generation has row, period and duration limits. Verify the library and fonts on the target host.

### Test delivery and payments

First validate Nova Poshta / Nova Post sandbox credentials, required domestic route, dictionaries, test shipment creation and tracking. API 2.0 and Nova Post have different contracts/keys. Support recipient, city/warehouse, weight/packages/value, server-configured sender, one active shipment per order, creation and manual refresh. If the official scenario is unavailable, label fake demonstrations explicitly; they do not prove external integration.

Use LiqPay sandbox Checkout: the server determines amount/currency and creates a uniquely identified attempt; the client opens the provider checkout in a browser. Card data goes only to LiqPay. A public HTTPS callback verifies signature, merchant, IDs, amount, currency and test mode. Check the signature algorithm against the selected API version and an actual sandbox callback; older examples may differ. Processing is idempotent; stale Pending cannot overwrite success. Browser return is not proof of payment; a server-side status refresh reconciles missing callbacks.

External calls run outside database transactions. Save operation state before calling and reconcile unknown results rather than blindly repeating creation. Keep credentials on the server. Callback records the financial fact; the first release uses an explicit user action through the workflow to change business status. Both integrations have real adapters and fakes for controlled tests.

## 11. WPF Client Requirements

The WPF application is a thin API client with login, overview, order list/details/editor, product/customer/user screens, reports and the following editors:

- Roles and access — Admin only: add, rename, block/unblock, delete with dependency explanations, and section-visibility matrix. Clearly show system/blocked status. Confirm destructive actions; update the local list only after API success.
- Workflow — Admin/Director only: forms/lists for states, transitions, allowed roles and typed rules; save draft, validate, show affected orders and publish. Protect against concurrent edits and silent loss of unsaved changes. A graph designer is outside scope.
- Reports — authorised users select templates/parameters and download server PDF; Admin/Director edit shared templates using simple field/block forms.

Build navigation from current effective permissions. Hide protected data and actions, refresh permissions on navigation refresh/403, close inaccessible sections and clear their data. API enforcement remains mandatory.

For drag-and-drop, start after the mouse movement threshold and use the order version visible before dragging. Highlight allowed targets; support empty columns, cancel outside the board and no-op in the same column. If several transitions lead to one target, ask which action to use; collect mandatory comments. Send one request and block duplicate actions for that card; complete the visual move only after server success. On timeout reread state/history; on 409 refresh current data. Refresh counts, totals and filters; ignore late responses from previous sessions. Preserve button-based transitions. Manual within-column ordering is outside scope.

Keep workflow, roles and report-builder logic in separate small view models rather than extending MainViewModel indefinitely.

## 12. Telegram Bot Requirements

BOT_INTEGRATION.md in the external Documentation folder is the required integration contract; it describes target behaviour, not existing API implementation.

The bot uses service account telegram-bot with protected system role TelegramBot. It can call only /api/bot/... plus /api/auth/login and /api/auth/refresh for authentication, and never obtains human administration rights. Access tokens last 15 minutes; refresh tokens last 14 days, rotate and revoke the session on replay. Store refresh/link-token hashes on the server. Validate current user/role state and credential changes. The bot keeps tokens in memory with single-flight refresh.

Implement GET /api/bot/users/{id}, POST /api/bot/users/{id}/link, DELETE /api/bot/users/{id}/link and POST /api/bot/users/{id}/report. Desktop APIs create short-lived one-time link tokens and support authorised unlinking. Resolve current identity/permissions per message. Use camelCase JSON, permissions orders.read, reports.read, reports.telegram.send and telegram.link through one catalog, and application/problem+json errors with contract codes.

Required commands: /start, /help, /me, /report, /unlink. Existing /stats and /orders_today require dedicated bot endpoints and permissions; the service token must not access ordinary business APIs. Reports support either {type: text, text: ...} or raw file bytes and Content-Disposition. Implement both branches; send server-generated PDF when available. No local fallback reports.

The bot stores no users, roles or credentials on disk. API owns all bindings, permissions and business data; API never talks to Telegram or receives its bot token. Handle only private text from real users and trust only from.id. Stay silent to unlinked accounts except /start instructions. Send plain text within Telegram limits; never log credentials or link payloads.

Verify TLS, do not forward tokens through redirects, and use bounded retries only for safe calls. Never automatically retry link redemption after timeout. Login limit: 5/minute/IP; redemption: 10/minute/Telegram id. Support a configured API-side IP allowlist. Prefer long polling; webhooks require secret-header validation and update-id deduplication. Deactivation/role blocks apply on the next API call even with an old JWT.

## 13. Epics

### Epic 1. Domain Model and Business Rules

Goal: implement the core entities, state machine, and business rules for order processing.

Expected result:

- domain model reflects the main business entities;
- invalid state transitions are blocked;
- business rules are not duplicated in client applications.

### Epic 2. Backend WebAPI

Goal: provide a stable API for orders, products, statistics, and reports.

Expected result:

- WPF client and Telegram bot work with data through WebAPI;
- API isolates business logic from client applications;
- business rule errors are returned in a clear format.

### Epic 3. Database and Infrastructure

Goal: provide reliable data storage and infrastructure for the remote database.

Expected result:

- users, roles, Telegram access, customers, products, prices, stock, orders, items, and status history are stored in the database;
- database structure is documented and reproducible through SQL scripts;
- configuration does not expose secrets in source code.

### Epic 4. WPF Desktop Client

Goal: implement a thin desktop client for daily order work and product administration.

Expected result:

- users can process orders without direct database access;
- administrators and directors can manage products through WPF;
- the interface displays the current order state and allowed actions.

### Epic 5. Telegram Bot and Reporting

Goal: implement a Telegram bot for statistics and management reports.

Expected result:

- the bot provides short order statistics;
- the director can receive a report by request;
- access to service information is restricted.

### Epic 6. Testing and Quality

Goal: verify business logic, API, client application, and integrations.

Expected result:

- critical business rules are covered by tests;
- main workflows are checked;
- the system is ready for Sprint Review demonstration.

### Epic 7. System extension

Complete interfaces for all PostgreSQL operations; Admin-only role lifecycle and visibility management; dynamic workflow APIs and Admin/Director editor; concurrency-safe board drag-and-drop; server PDF/templates/builder; test delivery and payments; regression tests and documentation. Epics 1–6 describe the foundation; their fixed states and roles are initial defaults extended by sections 4–12.

## 14. Sprint Plan

### Sprint 1. Core Domain and API Foundation

Goal: create the system foundation: domain model, user roles, state rules, basic API, database schema, and solution structure.

Planned work:

- create solution structure;
- implement `Order`, `Product`, `OrderItem`, and `User` entities;
- implement roles `Admin`, `Manager / Operator`, `Viewer`, `Director`;
- implement order state machine;
- implement basic authentication and order read/create endpoints;
- connect the database and prepare the initial table structure;
- prepare WPF project and basic navigation;
- prepare mock services for parallel WPF development;
- add unit tests for state transitions.

### Sprint 2. Order Processing and WPF Client

Goal: implement the full order lifecycle, product admin panel, and WPF workflows.

Planned work:

- implement endpoints for order items;
- implement state transition endpoints;
- implement status history;
- implement product viewing and administration endpoints;
- implement login, order list, order details, new order, and product admin screens in WPF;
- implement role-based restrictions in WPF and API;
- add API tests and manual test scenarios.

### Sprint 3. Telegram Bot, Statistics, Reports and Final QA

Goal: add Telegram bot, statistics, reports, and final testing.

Planned work:

- implement Telegram bot commands;
- implement statistics API;
- implement report generation;
- restrict access to management commands;
- run integration testing for WebAPI, WPF, product admin, and Telegram bot;
- fix defects;
- prepare final documentation and demo scenario.

### Extension sprints

Initial Sprints 1–3 above cover the foundation. Add:

| Sprint | Outcome |
| --- | --- |
| A | Check provider access; persistence interfaces, migrations, current permissions, role/visibility management and workflow model/APIs |
| B | WPF workflow editor, dynamic board and drag-and-drop, server PDF and report builder |
| C | Delivery, payments, end-to-end verification and documentation |

Current scope estimate: 30–49 person-days on PostgreSQL, excluding access delays. Additional backend, WPF and bot refresh/link/security work requires an additional estimate and is not included in that previous total. Allow a 20–30% learning-team contingency. Calendar sprint length depends on team availability. Verify external credentials at the beginning.

## 15. Definition of Ready

A task is ready for a sprint when:

- expected behavior is described;
- acceptance criteria are clear;
- dependencies are identified;
- affected system components are known;
- the task is small enough to complete within a sprint.

## 16. Definition of Done

A task is done when:

- functionality is implemented;
- code builds successfully;
- required tests are added or updated;
- functionality is checked manually or automatically;
- no critical defects remain;
- changes passed review;
- documentation is updated when needed.

## 17. Recommended Trello Labels

Work type labels:

- `Backend`
- `WPF Client`
- `Telegram Bot`
- `Database`
- `Authorization`
- `Admin Panel`
- `Testing`
- `Documentation`
- `Bug`
- `Blocked`

Sprint labels:

- `Sprint 1 - Core Domain and API Foundation`
- `Sprint 2 - Order Processing and WPF Client`
- `Sprint 3 - Telegram Bot, Statistics, Reports and Final QA`

## 18. Trello Board Structure

The Trello board must follow Agile Scrum workflow and contain these columns:

1. **Product Backlog** - all tasks, user stories, ideas, and requirements not yet planned for a sprint.
2. **Sprint Backlog / To Do** - tasks selected for the current sprint.
3. **In Progress** - tasks currently being worked on.
4. **Code Review / Review** - completed tasks that need review.
5. **Testing** - tasks being tested.
6. **Blocked** - tasks blocked by an issue or dependency.
7. **Done** - completed and verified tasks that meet the Definition of Done.

## 19. Risks

| Risk | Impact | Response plan |
| --- | --- | --- |
| Unclear state transition rules | High | Clarify with Product Owner before implementation |
| Problems with remote database access | Medium | Prepare local development connection or mock database |
| Telegram bot integration errors | Medium | Create separate tasks for tokens, permissions, and testing |
| Insufficient state machine testing | High | Cover transitions with unit tests |
| Too much business logic in WPF | Medium | Keep business logic in backend/application layer |
| Incorrect role restrictions | High | Check API endpoints for every role |
| Deleting products already used in orders | Medium | Use deactivation instead of physical deletion |

## 20. System and Extension Acceptance Criteria

- Core orders/products/customers, historical prices and status history work through the API under current permissions.
- Only Admin creates, renames, blocks/unblocks and deletes roles, assigns user roles and changes visibility. Direct API calls by other roles are denied.
- Blocking denies login, protected requests with old JWTs and Telegram access; blocked roles cannot be newly assigned. Unblocking follows the documented current-activity semantics.
- Referenced roles return 409 on deletion. System roles cannot be deleted, Admin cannot be blocked and the last active administrator is protected atomically.
- Denied visibility hides the WPF section and denies equivalent API, nested DTO, PDF and Telegram data. Viewing never implicitly grants editing.
- Admin and Director can create states/transitions/rules and allowed-role assignments, validate and publish from WPF. Other roles cannot edit workflow; invalid drafts do not change live orders.
- Custom states appear in board, filters and statistics without client rebuilding. Guards and final states are enforced.
- Simultaneous card moves produce one transition/history record. Conflicts, timeouts and repeats do not leave false UI state.
- Authorised users receive ready-made PDFs; Unicode fonts, multipage tables, dates and totals render correctly on the target host.
- Test shipment creation avoids duplicates and supports reconciliation. Fake demonstrations are clearly distinguished from provider sandbox execution.
- LiqPay sandbox success, failure, duplicate/invalid callback and reconciliation work with amount/currency checks. Test payments remain distinct; an invoice is not treated as proof of payment.
- All current PostgreSQL operations, including initial password seeding, use interfaces. Existing API/DTO contracts, transactions, rollback, concurrency, monetary precision and dates are verified on PostgreSQL.
- Login and refresh return both tokens and both expiry times. WPF renews access without another password prompt, uses one refresh for concurrent calls, retains the rotated token, ends expired/revoked sessions and ignores late refresh after sign-out.
- Bot API passes linking/replay/unlinking, refresh rotation, current-permission and deactivation scenarios from BOT_INTEGRATION.md; no plaintext server tokens or tokens on bot disk.
- The solution builds; relevant automated/manual checks pass; API contracts, demonstration instructions and specification copies are consistent.
