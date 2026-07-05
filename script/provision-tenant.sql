-- =============================================================================
-- Restaurant — Provision new tenant (PostgreSQL)
-- =============================================================================
-- Purpose:
--   Creates a tenant ready to use the system: roles/permissions, standard users,
--   starter catalog (2 product types, 2 products with recipes), 5 customers,
--   2 dining tables (Salón + Terraza), movement types, and default kitchen station.
--
-- Prerequisites:
--   1. EF migrations applied:  dotnet ef database update
--   2. Run once against the target database (edit CONFIGURATION below).
--   3. PostgreSQL 13+ (uses gen_random_uuid(); no pgcrypto required).
--   4. After running, copy product images — see IMAGE SETUP at the bottom.
--
-- Password note:
--   Users are created with a pre-computed BCrypt hash (default: Demo123!).
--   To use another password, replace p_password_hash (see comment in CONFIGURATION).
--
-- Login (all roles, same password):
--   administrator : idvillota@gmail.com
--   manager       : gerente@gmail.com
--   waitress      : mesera@gmail.com
--   cashier       : cajero@gmail.com
--
-- Multi-tenant note:
--   Emails are global in "Users". Re-running for another tenant reuses the same
--   user rows and only adds TenantUsers + roles for the new tenant.
--   Login with tenantSlug when the same email belongs to multiple tenants.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- CONFIGURATION — edit before running
-- -----------------------------------------------------------------------------
DO $provision$
DECLARE
    -- Tenant
    p_tenant_name              text := 'Restaurante Demo';
    p_tenant_slug              text := 'restaurante-demo';   -- unique, lowercase, no spaces
    p_timezone_id              text := 'America/Bogota';
    p_currency_code            text := 'COP';

    -- TenantSettings / billing profile
    p_trade_name               text := 'Restaurante Demo';
    p_legal_name               text := 'Restaurante Demo S.A.S.';
    p_tax_id                   text := '900123456-1';
    p_tax_regime               text := 'Régimen Simplificado';
    p_legal_representative     text := 'Iván Villota';
    p_address_line             text := 'Calle 10 # 20-30';
    p_city                     text := 'Bogotá';
    p_country                  text := 'Colombia';
    p_postal_code              text := '110111';
    p_phone                    text := '+57 300 000 0000';
    p_max_discount_percent     numeric(18,4) := 15;
    p_operational_cutoff_hour  integer := 4;
    p_impoconsumo_percent      numeric(18,4) := 8;

    -- Shared login password for all starter users (plaintext label for your reference)
    p_default_password         text := 'Demo123!';
    -- BCrypt hash for p_default_password (compatible with the API). Regenerate with:
    --   htpasswd -nbBC 11 '' 'YourPassword' | cut -d: -f2
    p_password_hash            text := '$2y$11$ARhnNBSjxwmZPw37dVi69Ootnsb1PekP/yjF181SaFqJ2JCHcE7cO';

    -- Standard user emails (same across every tenant)
    p_admin_email              text := 'idvillota@gmail.com';
    p_manager_email            text := 'gerente@gmail.com';
    p_waitress_email           text := 'mesera@gmail.com';
    p_cashier_email            text := 'cajero@gmail.com';

    -- Product image file extension (.jpg recommended)
    p_image_extension          text := '.jpg';

    -- Runtime
    v_now                      timestamptz := now() AT TIME ZONE 'UTC';
    v_tenant_id                uuid;
    v_role_admin_id            uuid;
    v_role_manager_id          uuid;
    v_role_waitress_id         uuid;
    v_role_cashier_id          uuid;
    v_user_admin_id            uuid;
    v_user_manager_id          uuid;
    v_user_waitress_id         uuid;
    v_user_cashier_id          uuid;
    v_tu_admin_id              uuid;
    v_tu_manager_id            uuid;
    v_tu_waitress_id           uuid;
    v_tu_cashier_id            uuid;
    v_cat_verduras_id          uuid := gen_random_uuid();
    v_cat_lacteos_id           uuid := gen_random_uuid();
    v_cat_frutas_id            uuid := gen_random_uuid();
    v_cat_insumos_id           uuid := gen_random_uuid();
    v_ing_tomate_id            uuid := gen_random_uuid();
    v_ing_queso_id             uuid := gen_random_uuid();
    v_ing_limon_id             uuid := gen_random_uuid();
    v_ing_azucar_id            uuid := gen_random_uuid();
    v_pt_platos_id             uuid := gen_random_uuid();
    v_pt_bebidas_id            uuid := gen_random_uuid();
    v_prod_pizza_id            uuid := gen_random_uuid();
    v_prod_limonada_id         uuid := gen_random_uuid();
    v_printer_default_id       uuid := gen_random_uuid();
    v_image_pizza              text;
    v_image_limonada           text;
    v_feature_count            integer;
BEGIN
    -- -------------------------------------------------------------------------
    -- Global feature catalog (idempotent; normally seeded by API startup)
    -- -------------------------------------------------------------------------
    INSERT INTO "Features" ("Id", "Code", "Name", "Module", "SortOrder", "CreatedAtUtc")
    VALUES
        ('f1000001-0001-4001-8001-000000000001', 'dashboard.view', 'Panel', 'General', 10, v_now),
        ('f1000021-0027-4027-8027-000000000021', 'dashboard.configure', 'Configurar panel', 'Administración', 11, v_now),
        ('f1000002-0002-4002-8002-000000000002', 'service.salon', 'Salón', 'Servicio', 20, v_now),
        ('f1000003-0003-4003-8003-000000000003', 'payments.checkout', 'Pagos', 'Servicio', 30, v_now),
        ('f1000004-0004-4004-8004-000000000004', 'reservations.manage', 'Reservas', 'Servicio', 40, v_now),
        ('f1000005-0005-4005-8005-000000000005', 'customers.manage', 'Clientes', 'Servicio', 50, v_now),
        ('f1000006-0006-4006-8006-000000000006', 'tables.manage', 'Mesas', 'Servicio', 60, v_now),
        ('f1000007-0007-4007-8007-000000000007', 'catalog.products', 'Productos', 'Menú', 70, v_now),
        ('f1000008-0008-4008-8008-000000000008', 'catalog.product-types', 'Tipos de producto', 'Menú', 80, v_now),
        ('f1000009-0009-4009-8009-000000000009', 'catalog.ingredient-categories', 'Categorías de ingrediente', 'Menú', 90, v_now),
        ('f100000a-0010-4010-8010-00000000000a', 'catalog.ingredients', 'Ingredientes', 'Menú', 100, v_now),
        ('f1000019-0025-4025-8025-000000000019', 'inventory.ingredient-movement-types', 'Tipos de movimiento', 'Inventario', 102, v_now),
        ('f1000020-0026-4026-8026-000000000020', 'inventory.ingredient-movements', 'Movimientos de inventario', 'Inventario', 103, v_now),
        ('f1000014-0020-4020-8020-000000000014', 'catalog.public_menu_qr', 'Código QR menú', 'Menú', 105, v_now),
        ('f100000b-0011-4011-8011-00000000000b', 'settings.tenant', 'Configuración', 'Administración', 110, v_now),
        ('f100000c-0012-4012-8012-00000000000c', 'procurement.purchases', 'Compras', 'Administración', 120, v_now),
        ('f100000d-0013-4013-8013-00000000000d', 'procurement.providers', 'Proveedores', 'Administración', 130, v_now),
        ('f100000e-0014-4014-8014-00000000000e', 'organization.employees', 'Empleados', 'Administración', 140, v_now),
        ('f100000f-0015-4015-8015-00000000000f', 'organization.team', 'Invitar equipo', 'Administración', 150, v_now),
        ('f1000010-0016-4016-8016-000000000010', 'organization.roles', 'Roles y permisos', 'Administración', 160, v_now),
        ('f1000011-0017-4017-8017-000000000011', 'cashier.shifts', 'Turnos de caja', 'Servicio', 35, v_now),
        ('f1000012-0018-4018-8018-000000000012', 'reports.daily_closure', 'Cierre diario', 'Reportes', 165, v_now),
        ('f1000013-0019-4019-8019-000000000013', 'reports.strategic_ai', 'Informe IA', 'Reportes', 170, v_now),
        ('f1000015-0021-4021-8021-000000000015', 'reports.sales', 'Reporte de ventas', 'Reportes', 175, v_now),
        ('f1000016-0022-4022-8022-000000000016', 'reports.ingredients', 'Reporte de ingredientes', 'Reportes', 176, v_now),
        ('f1000017-0023-4023-8023-000000000017', 'reports.purchases', 'Reporte de compras', 'Reportes', 177, v_now),
        ('f1000018-0024-4024-8024-000000000018', 'reports.sales_by_date', 'Ventas por fecha', 'Reportes', 178, v_now)
    ON CONFLICT ("Code") DO NOTHING;

    SELECT COUNT(*) INTO v_feature_count FROM "Features";
    IF v_feature_count < 27 THEN
        RAISE EXCEPTION 'Features catalog incomplete (% rows). Run API once or fix seed above.', v_feature_count;
    END IF;

    IF EXISTS (SELECT 1 FROM "Tenants" WHERE "Slug" = p_tenant_slug) THEN
        RAISE EXCEPTION 'Tenant slug "%" already exists. Choose another p_tenant_slug.', p_tenant_slug;
    END IF;

    v_tenant_id := gen_random_uuid();
    v_image_pizza := replace(v_tenant_id::text, '-', '') || '/' || replace(v_prod_pizza_id::text, '-', '') || p_image_extension;
    v_image_limonada := replace(v_tenant_id::text, '-', '') || '/' || replace(v_prod_limonada_id::text, '-', '') || p_image_extension;

    -- -------------------------------------------------------------------------
    -- Tenant + settings
    -- -------------------------------------------------------------------------
    INSERT INTO "Tenants" ("Id", "Name", "Slug", "TimeZoneId", "CurrencyCode", "IsActive", "CreatedAtUtc")
    VALUES (v_tenant_id, p_tenant_name, p_tenant_slug, p_timezone_id, p_currency_code, true, v_now);

    INSERT INTO "TenantSettings" (
        "TenantId", "MaxDiscountPercent", "OperationalDayCutoffHour",
        "TradeName", "LegalName", "TaxRegime", "TaxId", "LegalRepresentative",
        "AddressLine", "City", "Country", "PostalCode", "Phone",
        "DianResolutionFrom", "DianResolutionTo", "DianNextConsecutive", "ImpoconsumoPercent"
    )
    VALUES (
        v_tenant_id, p_max_discount_percent, p_operational_cutoff_hour,
        p_trade_name, p_legal_name, p_tax_regime, p_tax_id, p_legal_representative,
        p_address_line, p_city, p_country, p_postal_code, p_phone,
        0, 0, 0, p_impoconsumo_percent
    );

    -- -------------------------------------------------------------------------
    -- Roles
    -- -------------------------------------------------------------------------
    v_role_admin_id := gen_random_uuid();
    v_role_manager_id := gen_random_uuid();
    v_role_waitress_id := gen_random_uuid();
    v_role_cashier_id := gen_random_uuid();

    INSERT INTO "Roles" ("Id", "TenantId", "Name", "NormalizedName", "CreatedAtUtc")
    VALUES
        (v_role_admin_id,    v_tenant_id, 'Administrator', 'ADMINISTRATOR', v_now),
        (v_role_manager_id,  v_tenant_id, 'Manager',       'MANAGER',       v_now),
        (v_role_waitress_id, v_tenant_id, 'Waitress',      'WAITRESS',      v_now),
        (v_role_cashier_id,  v_tenant_id, 'Cashier',       'CASHIER',       v_now);

    -- -------------------------------------------------------------------------
    -- Users (global emails — reuse on subsequent tenants)
    -- -------------------------------------------------------------------------
    INSERT INTO "Users" ("Id", "Email", "NormalizedEmail", "PasswordHash", "DisplayName", "CreatedAtUtc")
    VALUES (gen_random_uuid(), p_admin_email, upper(p_admin_email), p_password_hash, 'Administrador', v_now)
    ON CONFLICT ("NormalizedEmail") DO NOTHING;
    SELECT "Id" INTO v_user_admin_id FROM "Users" WHERE "NormalizedEmail" = upper(p_admin_email);

    INSERT INTO "Users" ("Id", "Email", "NormalizedEmail", "PasswordHash", "DisplayName", "CreatedAtUtc")
    VALUES (gen_random_uuid(), p_manager_email, upper(p_manager_email), p_password_hash, 'Gerente', v_now)
    ON CONFLICT ("NormalizedEmail") DO NOTHING;
    SELECT "Id" INTO v_user_manager_id FROM "Users" WHERE "NormalizedEmail" = upper(p_manager_email);

    INSERT INTO "Users" ("Id", "Email", "NormalizedEmail", "PasswordHash", "DisplayName", "CreatedAtUtc")
    VALUES (gen_random_uuid(), p_waitress_email, upper(p_waitress_email), p_password_hash, 'Mesera', v_now)
    ON CONFLICT ("NormalizedEmail") DO NOTHING;
    SELECT "Id" INTO v_user_waitress_id FROM "Users" WHERE "NormalizedEmail" = upper(p_waitress_email);

    INSERT INTO "Users" ("Id", "Email", "NormalizedEmail", "PasswordHash", "DisplayName", "CreatedAtUtc")
    VALUES (gen_random_uuid(), p_cashier_email, upper(p_cashier_email), p_password_hash, 'Cajero', v_now)
    ON CONFLICT ("NormalizedEmail") DO NOTHING;
    SELECT "Id" INTO v_user_cashier_id FROM "Users" WHERE "NormalizedEmail" = upper(p_cashier_email);

    -- -------------------------------------------------------------------------
    -- Tenant memberships + role assignments
    -- -------------------------------------------------------------------------
    v_tu_admin_id := gen_random_uuid();
    v_tu_manager_id := gen_random_uuid();
    v_tu_waitress_id := gen_random_uuid();
    v_tu_cashier_id := gen_random_uuid();

    INSERT INTO "TenantUsers" ("Id", "TenantId", "UserId", "IsActive", "BrandTheme", "ColorScheme", "CreatedAtUtc")
    VALUES
        (v_tu_admin_id,    v_tenant_id, v_user_admin_id,    true, 'operations', 'auto', v_now),
        (v_tu_manager_id,  v_tenant_id, v_user_manager_id,  true, 'operations', 'auto', v_now),
        (v_tu_waitress_id, v_tenant_id, v_user_waitress_id, true, 'operations', 'auto', v_now),
        (v_tu_cashier_id,  v_tenant_id, v_user_cashier_id,  true, 'operations', 'auto', v_now);

    INSERT INTO "TenantUserRoles" ("Id", "TenantId", "TenantUserId", "RoleId", "CreatedAtUtc")
    VALUES
        (gen_random_uuid(), v_tenant_id, v_tu_admin_id,    v_role_admin_id,    v_now),
        (gen_random_uuid(), v_tenant_id, v_tu_manager_id,  v_role_manager_id,  v_now),
        (gen_random_uuid(), v_tenant_id, v_tu_waitress_id, v_role_waitress_id, v_now),
        (gen_random_uuid(), v_tenant_id, v_tu_cashier_id,  v_role_cashier_id,  v_now);

    -- -------------------------------------------------------------------------
    -- Role permissions (matches FeatureCatalog.DefaultFeaturesByRole)
    -- -------------------------------------------------------------------------
    INSERT INTO "RoleFeatures" ("Id", "TenantId", "RoleId", "FeatureId", "CreatedAtUtc")
    SELECT gen_random_uuid(), v_tenant_id, v_role_admin_id, f."Id", v_now
    FROM "Features" f;

    INSERT INTO "RoleFeatures" ("Id", "TenantId", "RoleId", "FeatureId", "CreatedAtUtc")
    SELECT gen_random_uuid(), v_tenant_id, v_role_manager_id, f."Id", v_now
    FROM "Features" f
    WHERE f."Code" IN (
        'dashboard.view', 'dashboard.configure', 'service.salon', 'payments.checkout', 'cashier.shifts',
        'reservations.manage', 'customers.manage', 'tables.manage',
        'catalog.products', 'catalog.product-types', 'catalog.ingredient-categories', 'catalog.ingredients',
        'inventory.ingredient-movement-types', 'inventory.ingredient-movements', 'catalog.public_menu_qr',
        'settings.tenant', 'procurement.purchases', 'procurement.providers',
        'organization.employees', 'organization.team',
        'reports.daily_closure', 'reports.sales', 'reports.ingredients', 'reports.purchases', 'reports.sales_by_date'
    );

    INSERT INTO "RoleFeatures" ("Id", "TenantId", "RoleId", "FeatureId", "CreatedAtUtc")
    SELECT gen_random_uuid(), v_tenant_id, v_role_waitress_id, f."Id", v_now
    FROM "Features" f
    WHERE f."Code" IN (
        'dashboard.view', 'service.salon', 'reservations.manage', 'customers.manage', 'catalog.public_menu_qr'
    );

    INSERT INTO "RoleFeatures" ("Id", "TenantId", "RoleId", "FeatureId", "CreatedAtUtc")
    SELECT gen_random_uuid(), v_tenant_id, v_role_cashier_id, f."Id", v_now
    FROM "Features" f
    WHERE f."Code" IN (
        'dashboard.view', 'service.salon', 'payments.checkout', 'cashier.shifts',
        'customers.manage', 'catalog.public_menu_qr', 'reports.sales', 'reports.sales_by_date'
    );

    -- -------------------------------------------------------------------------
    -- Ingredient movement types (5 defaults)
    -- -------------------------------------------------------------------------
    INSERT INTO "IngredientMovementTypes" ("Id", "TenantId", "Name", "Description", "IsInput", "SortOrder", "IsActive", "CreatedAtUtc")
    VALUES
        (gen_random_uuid(), v_tenant_id, 'Ingreso por regalo', 'Stock recibido sin costo de compra', true,  10, true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Ajuste positivo',    'Corrección por conteo físico (más stock)', true,  20, true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Salida por baja',    'Descarte intencional de producto', false, 30, true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Salida por pérdida', 'Merma o deterioro no planificado', false, 40, true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Ajuste negativo',    'Corrección por conteo físico (menos stock)', false, 50, true, v_now);

    -- -------------------------------------------------------------------------
    -- Default kitchen printer station
    -- -------------------------------------------------------------------------
    INSERT INTO "PrinterStations" ("Id", "TenantId", "Name", "Code", "IsActive", "SortOrder", "CreatedAtUtc")
    VALUES (v_printer_default_id, v_tenant_id, 'Cocina (predeterminada)', 'DEFAULT', true, 0, v_now);

    -- -------------------------------------------------------------------------
    -- Catalog: 4 ingredient categories, 4 ingredients, 2 product types, 2 products
    -- CompositionType: 0 = Prepared
    -- IngredientUnit: 0 = Unit, 2 = Gram
    -- -------------------------------------------------------------------------
    INSERT INTO "IngredientCategories" ("Id", "TenantId", "Name", "Description", "SortOrder", "IsActive", "CreatedAtUtc")
    VALUES
        (v_cat_verduras_id, v_tenant_id, 'Verduras',  'Categoría: verduras',  0, true, v_now),
        (v_cat_lacteos_id,  v_tenant_id, 'Lácteos',   'Categoría: lácteos',   1, true, v_now),
        (v_cat_frutas_id,   v_tenant_id, 'Frutas',    'Categoría: frutas',    2, true, v_now),
        (v_cat_insumos_id,  v_tenant_id, 'Insumos',   'Categoría: insumos',   3, true, v_now);

    INSERT INTO "Ingredients" ("Id", "TenantId", "IngredientCategoryId", "Name", "Unit", "IsActive", "StockQuantity", "ReorderLevel", "UnitCost", "CreatedAtUtc")
    VALUES
        (v_ing_tomate_id,  v_tenant_id, v_cat_verduras_id, 'Tomate',           2, true, 10000, 2000,  8,    v_now),
        (v_ing_queso_id,   v_tenant_id, v_cat_lacteos_id,  'Queso mozzarella', 2, true, 8000,  1500,  22,   v_now),
        (v_ing_limon_id,   v_tenant_id, v_cat_frutas_id,   'Limón',            0, true, 200,   40,    500,  v_now),
        (v_ing_azucar_id,  v_tenant_id, v_cat_insumos_id,  'Azúcar',           2, true, 5000,  1000,  5,    v_now);

    INSERT INTO "ProductTypes" ("Id", "TenantId", "Name", "Description", "SortOrder", "IsActive", "CreatedAtUtc")
    VALUES
        (v_pt_platos_id,  v_tenant_id, 'Platos',  'Platos principales', 0, true, v_now),
        (v_pt_bebidas_id, v_tenant_id, 'Bebidas', 'Bebidas',            1, true, v_now);

    INSERT INTO "Products" (
        "Id", "TenantId", "ProductTypeId", "CompositionType", "Name", "Description",
        "Sku", "UnitPrice", "IsActive", "ImagePath", "CreatedAtUtc"
    )
    VALUES
        (v_prod_pizza_id,    v_tenant_id, v_pt_platos_id,  0, 'Pizza margarita',    'Pizza clásica con tomate y queso', 'PLT-001', 32000, true, v_image_pizza,    v_now),
        (v_prod_limonada_id, v_tenant_id, v_pt_bebidas_id, 0, 'Limonada natural',   'Bebida refrescante',               'BEB-001', 8000,  true, v_image_limonada, v_now);

    INSERT INTO "ProductIngredients" ("Id", "TenantId", "ProductId", "IngredientId", "Quantity", "CreatedAtUtc")
    VALUES
        (gen_random_uuid(), v_tenant_id, v_prod_pizza_id,    v_ing_tomate_id, 180,  v_now),
        (gen_random_uuid(), v_tenant_id, v_prod_pizza_id,    v_ing_queso_id,  120,  v_now),
        (gen_random_uuid(), v_tenant_id, v_prod_limonada_id, v_ing_limon_id,  2,    v_now),
        (gen_random_uuid(), v_tenant_id, v_prod_limonada_id, v_ing_azucar_id, 25,   v_now);

    -- Map product types to default kitchen station
    INSERT INTO "ProductTypePrinterMappings" ("Id", "TenantId", "ProductTypeId", "PrinterStationId", "CreatedAtUtc")
    VALUES
        (gen_random_uuid(), v_tenant_id, v_pt_platos_id,  v_printer_default_id, v_now),
        (gen_random_uuid(), v_tenant_id, v_pt_bebidas_id, v_printer_default_id, v_now);

    -- -------------------------------------------------------------------------
    -- Customers (5): 4 dummy + consumidor final with TaxId 222222222
    -- -------------------------------------------------------------------------
    INSERT INTO "Customers" ("Id", "TenantId", "Name", "Email", "Phone", "TaxId", "IsActive", "CreatedAtUtc")
    VALUES
        (gen_random_uuid(), v_tenant_id, 'Consumidor final', NULL,                  NULL,              '222222222',            true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Ana García',       'ana.garcia@mail.com', '+57 301 111 1111', '1012345678',      true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Carlos Ruiz',      'carlos.ruiz@mail.com','+57 302 222 2222', '1023456789',      true, v_now),
        (gen_random_uuid(), v_tenant_id, 'María López',      'maria.lopez@mail.com','+57 303 333 3333', '1034567890',      true, v_now),
        (gen_random_uuid(), v_tenant_id, 'Pedro Sánchez',    'pedro.sanchez@mail.com','+57 304 444 4444','1045678901',    true, v_now);

    -- -------------------------------------------------------------------------
    -- Dining tables (2): one in Salón, one in Terraza
    -- Status: 0 = Available, 1 = Busy, 2 = Reserved
    -- -------------------------------------------------------------------------
    INSERT INTO "DiningTables" (
        "Id", "TenantId", "Code", "Capacity", "Zone", "LayoutX", "LayoutY", "Status", "IsActive", "CreatedAtUtc"
    )
    VALUES
        (gen_random_uuid(), v_tenant_id, 'M-01', 4, 'Salón',   10, 10, 0, true, v_now),
        (gen_random_uuid(), v_tenant_id, 'M-02', 4, 'Terraza', 34, 10, 0, true, v_now);

    RAISE NOTICE 'Tenant provisioned successfully.';
    RAISE NOTICE '  TenantId : %', v_tenant_id;
    RAISE NOTICE '  Slug     : %', p_tenant_slug;
    RAISE NOTICE '  Password : % (all roles)', p_default_password;
    RAISE NOTICE '  Images   : copy files to uploads/products/% and uploads/products/%',
        v_image_pizza, v_image_limonada;
END
$provision$;

-- =============================================================================
-- IMAGE SETUP (manual step after SQL)
-- =============================================================================
-- The API serves images from:  uploads/products/{tenantId}/{productId}.jpg
-- (relative to the API content root; public URL /media/products/...)
--
-- Suggested source files (if you have the dev seed assets):
--   seed-data/product-images/pizza-margarita.jpg  -> pizza product path from NOTICE
--   seed-data/product-images/limonada.jpg         -> limonada product path from NOTICE
--
-- Example (replace paths from the NOTICE output):
--   mkdir -p uploads/products/<tenantIdN>
--   cp seed-data/product-images/pizza-margarita.jpg uploads/products/<tenantIdN>/<productIdN>.jpg
--   cp seed-data/product-images/limonada.jpg        uploads/products/<tenantIdN>/<productIdN>.jpg
-- =============================================================================
