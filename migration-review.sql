CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE customers (
    id uuid NOT NULL,
    name character varying(200) NOT NULL,
    plan integer NOT NULL,
    balance numeric(18,2) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_customers" PRIMARY KEY (id)
);

CREATE TABLE invoices (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    period_start date NOT NULL,
    period_end date NOT NULL,
    total numeric(18,2) NOT NULL,
    status integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_invoices" PRIMARY KEY (id),
    CONSTRAINT "FK_invoices_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT
);

CREATE TABLE operations (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    type integer NOT NULL,
    amount numeric(18,2) NOT NULL,
    status integer NOT NULL,
    request_hash character varying(200) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    invoice_id uuid,
    CONSTRAINT "PK_operations" PRIMARY KEY (id),
    CONSTRAINT "FK_operations_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_operations_invoices_invoice_id" FOREIGN KEY (invoice_id) REFERENCES invoices (id) ON DELETE RESTRICT
);

CREATE TABLE invoice_items (
    invoice_id uuid NOT NULL,
    operation_id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    CONSTRAINT "PK_invoice_items" PRIMARY KEY (invoice_id, operation_id),
    CONSTRAINT "FK_invoice_items_invoices_invoice_id" FOREIGN KEY (invoice_id) REFERENCES invoices (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_invoice_items_operations_operation_id" FOREIGN KEY (operation_id) REFERENCES operations (id) ON DELETE RESTRICT
);

CREATE TABLE ledger_entries (
    id uuid NOT NULL,
    operation_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    type integer NOT NULL,
    amount numeric(18,2) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ledger_entries" PRIMARY KEY (id),
    CONSTRAINT "FK_ledger_entries_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ledger_entries_operations_operation_id" FOREIGN KEY (operation_id) REFERENCES operations (id) ON DELETE RESTRICT
);

CREATE INDEX ix_customers_plan ON customers (plan);

CREATE UNIQUE INDEX ux_invoice_items_operation_id ON invoice_items (operation_id);

CREATE INDEX ix_invoices_status_created_at ON invoices (status, created_at);

CREATE UNIQUE INDEX ux_invoices_customer_period ON invoices (customer_id, period_start, period_end);

CREATE INDEX ix_ledger_entries_customer_created_at ON ledger_entries (customer_id, created_at);

CREATE UNIQUE INDEX ux_ledger_entries_operation_type ON ledger_entries (operation_id, type);

CREATE INDEX ix_operations_customer_created_at ON operations (customer_id, created_at);

CREATE INDEX ix_operations_invoice_id ON operations (invoice_id);

CREATE INDEX ix_operations_status_created_at ON operations (status, created_at);

CREATE UNIQUE INDEX ux_operations_customer_request_hash ON operations (customer_id, request_hash);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261018210853_InitialPostgresSchema', '9.0.4');

COMMIT;

