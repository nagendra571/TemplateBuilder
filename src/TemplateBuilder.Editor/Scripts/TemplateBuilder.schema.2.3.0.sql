IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    CREATE TABLE [Templates] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [TemplateType] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CurrentVersionId] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    CREATE TABLE [TemplateVersions] (
        [Id] int NOT NULL IDENTITY,
        [TemplateId] int NOT NULL,
        [VersionNumber] int NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [ChangeComment] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_TemplateVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TemplateVersions_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Templates_CurrentVersionId] ON [Templates] ([CurrentVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Templates_Name] ON [Templates] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TemplateVersions_TemplateId_VersionNumber] ON [TemplateVersions] ([TemplateId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    ALTER TABLE [Templates] ADD CONSTRAINT [FK_Templates_TemplateVersions_CurrentVersionId] FOREIGN KEY ([CurrentVersionId]) REFERENCES [TemplateVersions] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513222213_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513222213_InitialCreate', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601180334_AddSnippets'
)
BEGIN
    CREATE TABLE [Snippets] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(300) NULL,
        [Body] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Snippets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601180334_AddSnippets'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Snippets_Name] ON [Snippets] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601180334_AddSnippets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601180334_AddSnippets', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819170757_AddSampleDataToTemplate'
)
BEGIN
    ALTER TABLE [Templates] ADD [SampleData] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819170757_AddSampleDataToTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819170757_AddSampleDataToTemplate', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821132416_AddVersionIsActive'
)
BEGIN
    ALTER TABLE [TemplateVersions] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821132416_AddVersionIsActive'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821132416_AddVersionIsActive', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821141223_AddLifecycleOps'
)
BEGIN
    ALTER TABLE [Templates] ADD [ExternalKey] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821141223_AddLifecycleOps'
)
BEGIN
    ALTER TABLE [Templates] ADD [SourceView] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821141223_AddLifecycleOps'
)
BEGIN
    ALTER TABLE [Templates] ADD [SourceViewSnapshot] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821141223_AddLifecycleOps'
)
BEGIN
    UPDATE dbo.Templates SET ExternalKey = NEWID() WHERE ExternalKey = '00000000-0000-0000-0000-000000000000'
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821141223_AddLifecycleOps'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Templates_ExternalKey] ON [Templates] ([ExternalKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821141223_AddLifecycleOps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821141223_AddLifecycleOps', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822020824_AddAuditLog'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [EntityType] nvarchar(20) NOT NULL,
        [EntityId] int NOT NULL,
        [Action] nvarchar(40) NOT NULL,
        [Actor] nvarchar(200) NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [BeforeState] nvarchar(4000) NULL,
        [AfterState] nvarchar(4000) NULL,
        [Comment] nvarchar(1000) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822020824_AddAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId_OccurredAt] ON [AuditLogs] ([EntityType], [EntityId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822020824_AddAuditLog'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OccurredAt] ON [AuditLogs] ([OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822020824_AddAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822020824_AddAuditLog', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904164146_AddSubjectToTemplateVersions'
)
BEGIN
    ALTER TABLE [TemplateVersions] ADD [Subject] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260904164146_AddSubjectToTemplateVersions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260904164146_AddSubjectToTemplateVersions', N'10.0.11');
END;

COMMIT;
GO

