CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE TABLE customers (
        id uuid NOT NULL,
        name character varying(200) NOT NULL,
        plan integer NOT NULL,
        balance numeric(18,2) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_customers" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE TABLE invoice_items (
        invoice_id uuid NOT NULL,
        operation_id uuid NOT NULL,
        amount numeric(18,2) NOT NULL,
        CONSTRAINT "PK_invoice_items" PRIMARY KEY (invoice_id, operation_id),
        CONSTRAINT "FK_invoice_items_invoices_invoice_id" FOREIGN KEY (invoice_id) REFERENCES invoices (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_invoice_items_operations_operation_id" FOREIGN KEY (operation_id) REFERENCES operations (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE INDEX ix_customers_plan ON customers (plan);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE UNIQUE INDEX ux_invoice_items_operation_id ON invoice_items (operation_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE INDEX ix_invoices_status_created_at ON invoices (status, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE UNIQUE INDEX ux_invoices_customer_period ON invoices (customer_id, period_start, period_end);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE INDEX ix_ledger_entries_customer_created_at ON ledger_entries (customer_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE UNIQUE INDEX ux_ledger_entries_operation_type ON ledger_entries (operation_id, type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE INDEX ix_operations_customer_created_at ON operations (customer_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE INDEX ix_operations_invoice_id ON operations (invoice_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE INDEX ix_operations_status_created_at ON operations (status, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    CREATE UNIQUE INDEX ux_operations_customer_request_hash ON operations (customer_id, request_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261018210853_InitialPostgresSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261018210853_InitialPostgresSchema', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020034508_AddOutboxMessage') THEN
    CREATE TABLE outbox_messages (
        "Id" uuid NOT NULL,
        "MessageType" character varying(200) NOT NULL,
        "AggregateId" uuid,
        "Payload" jsonb NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "AttemptCount" integer NOT NULL DEFAULT 0,
        "NextAttemptAt" timestamp with time zone,
        "LastError" text,
        CONSTRAINT "PK_outbox_messages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020034508_AddOutboxMessage') THEN
    CREATE INDEX ix_outbox_messages_pending ON outbox_messages ("ProcessedAt", "NextAttemptAt", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020034508_AddOutboxMessage') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020034508_AddOutboxMessage', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025131850_AlignLedgerEntryModel') THEN
    ALTER TABLE operations ADD external_operation_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025131850_AlignLedgerEntryModel') THEN
    ALTER TABLE operations ADD invoiced_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025131850_AlignLedgerEntryModel') THEN
    ALTER TABLE operations ADD refunded_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025131850_AlignLedgerEntryModel') THEN
    ALTER TABLE ledger_entries ADD description character varying(250) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025131850_AlignLedgerEntryModel') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025131850_AlignLedgerEntryModel', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026213802_AddOutboxMessageLock') THEN
    ALTER TABLE outbox_messages ADD "LockId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026213802_AddOutboxMessageLock') THEN
    ALTER TABLE outbox_messages ADD "LockedUntil" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026213802_AddOutboxMessageLock') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026213802_AddOutboxMessageLock', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026224020_AddInboxMessage') THEN
    CREATE TABLE inbox_messages (
        id uuid NOT NULL,
        consumer character varying(200) NOT NULL,
        processed_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_inbox_messages" PRIMARY KEY (id, consumer)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026224020_AddInboxMessage') THEN
    CREATE INDEX ix_inbox_messages_processed_at ON inbox_messages (processed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026224020_AddInboxMessage') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026224020_AddInboxMessage', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030025249_AddUniqueExternalOperationId') THEN
    CREATE UNIQUE INDEX ux_operations_external_operation_id ON operations (external_operation_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030025249_AddUniqueExternalOperationId') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030025249_AddUniqueExternalOperationId', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002013_AddBalanceCredits') THEN
    CREATE TABLE balance_credits (
        id uuid NOT NULL,
        customer_id uuid NOT NULL,
        external_reference character varying(200) NOT NULL,
        amount numeric(18,2) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_balance_credits" PRIMARY KEY (id),
        CONSTRAINT "FK_balance_credits_customers_customer_id" FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002013_AddBalanceCredits') THEN
    CREATE INDEX "IX_balance_credits_customer_id" ON balance_credits (customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002013_AddBalanceCredits') THEN
    CREATE UNIQUE INDEX ux_balance_credits_external_reference ON balance_credits (external_reference);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002013_AddBalanceCredits') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103002013_AddBalanceCredits', '9.0.4');
    END IF;
END $EF$;
COMMIT;

