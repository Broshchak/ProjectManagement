# Technical Specification

## Project

**Name:** Orders Processing System  
**Type:** C# WebAPI application, thin WPF desktop client, Telegram bot  
**Project management methodology:** Agile Scrum  
**Planning scope:** 3 sprints

## 1. Project Goal

The goal is to develop a software system for processing product orders stored in a PostgreSQL database on a remote server.

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

- C# WebAPI backend;
- order and product domain model;
- admin module for product management;
- basic authentication and user roles;
- order state machine;
- business rule validation;
- work with a remote PostgreSQL database;
- WPF client for order processing;
- Telegram bot for information, statistics, and reports;
- basic logging, error handling, and validation;
- testing of key business rules.

### Outside the initial MVP

- full payment system;
- integration with real delivery services;
- multi-level CRM;
- complex staff management module;
- enterprise-level audit of every user action;
- advanced BI analytics;
- mobile application.

## 5. Main Entities

### Role

Contains an identifier, role code, and role name.

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

The actual database structure must match the SQL scripts:

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

## 7. User Roles and Access Rights

| Role | Rights |
| --- | --- |
| **Admin** | Product management, order viewing and editing, state transitions, statistics, user management |
| **Manager / Operator** | Creating and processing orders, viewing products, executing allowed order actions |
| **Viewer** | View-only access to products, orders, and statistics |
| **Director** | Full view access, product and order management, statistics and reports through Telegram; no user role management |

Access rules:

- adding, editing, deactivating products, and changing stock are available to `Admin` and `Director`;
- creating and processing orders is available to `Admin`, `Manager / Operator`, and `Director`;
- viewing orders is available to all roles;
- viewing statistics is available to `Admin`, `Viewer`, and `Director`;
- viewing users is available to `Admin` and `Director`, but adding users or changing roles is available only to `Admin`;
- Telegram reporting commands are available only to active users with `telegram_allowed = TRUE` and a filled `telegram_user_id`;
- a user without the required role receives an access denial without data changes.

## 8. Order States

An order can have one of these states:

- **NewOrder** - new order;
- **Registered** - registered order;
- **Granted** - approved order;
- **Shipped** - shipped order;
- **Invoiced** - paid or invoiced order;
- **Cancelled** - cancelled order.

Allowed transitions:

| Current state | Next state | Description |
| --- | --- | --- |
| NewOrder | Registered | Register a new order |
| NewOrder | Cancelled | Cancel a new order |
| Registered | Granted | Approve a registered order |
| Registered | Cancelled | Cancel a registered order |
| Granted | Shipped | Ship an approved order |
| Granted | Cancelled | Cancel an approved order |
| Shipped | Invoiced | Mark as paid or invoiced after shipping |

Forbidden transitions:

- shipping an order that was not registered and approved;
- approving a `NewOrder` without registration;
- changing `Cancelled` to any other state;
- changing `Invoiced` to any other state in the MVP;
- skipping required intermediate states.

## 9. Business Rules

- A product can be added only to an order in `NewOrder` or `Registered`.
- A product cannot be added to an order in `Granted`, `Shipped`, `Invoiced`, or `Cancelled`.
- Only active products can be added to an order.
- A product with an invalid price or unavailable quantity cannot be added.
- Product data in an existing order item must not be changed automatically after catalog changes.
- An order cannot be registered without at least one item.
- An order cannot be approved if product quantity is invalid or unavailable.
- An order cannot be shipped before approval.
- A cancelled order cannot be edited.
- All successful state changes must be stored in status history.

## 10. WebAPI Requirements

The backend must provide endpoints for:

- authentication and current user role;
- viewing products;
- product administration;
- creating and viewing orders;
- adding, changing, and deleting order items;
- order state transitions: `register`, `grant`, `ship`, `invoice`, `cancel`;
- order statistics;
- report data for Telegram.

API responses must use DTOs and must not expose internal database implementation details. Business rule errors must have clear messages and suitable HTTP status codes.

## 11. WPF Client Requirements

The WPF application is a thin client and must not duplicate backend business logic.

Required screens:

- login screen;
- dashboard or overview screen;
- order list screen;
- order details screen;
- new order form;
- product administration screen;
- reports/statistics screen if needed for demonstration.

The UI must show or hide actions according to the current user role and the current order state. Validation errors and API errors must be shown in a clear form.

## 12. Telegram Bot Requirements

The Telegram bot must support:

- `/start`;
- `/help`;
- `/stats`;
- `/orders_today`;
- `/report`.

The bot must receive statistics and report data through WebAPI. Management commands must be restricted to allowed users.

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

## 20. MVP Acceptance Criteria

- A user can create an order.
- A user can add products to a new or registered order.
- An administrator and director can add, edit, and deactivate products.
- Users with different roles have different view and edit rights.
- The system blocks forbidden state actions.
- A user can process an order through the allowed lifecycle.
- A cancelled order cannot be edited.
- The WPF client works with WebAPI.
- The Telegram bot returns statistics.
- The director can receive a report through Telegram.
- Main business rules are covered by tests.
