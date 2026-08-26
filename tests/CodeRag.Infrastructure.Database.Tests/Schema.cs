namespace CodeRag.Infrastructure.Database.Tests;

/// <summary>
/// Schema DDL mirroring the production tables, applied directly by <see cref="PostgresFixture"/>.
/// This API never runs migrations, so tests own their own schema setup against a disposable container.
/// </summary>
internal static class Schema
{
    public const string Ddl = """
        CREATE EXTENSION IF NOT EXISTS vector;

        CREATE TABLE public.embedding_models (
            id int8 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            provider text NOT NULL,
            model text NOT NULL,
            dimensions int4 NOT NULL,
            normalized bool NOT NULL,
            configuration jsonb NULL,
            created_at timestamptz DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
            CONSTRAINT ux_embedding_models_provider_model_dimensions UNIQUE (provider, model, dimensions)
        );

        CREATE TABLE public.projects (
            id int8 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            name text NOT NULL,
            created_at timestamptz DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
            CONSTRAINT ux_projects_name UNIQUE (name)
        );

        CREATE TABLE public.code_documents (
            id int8 GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            document_id text NOT NULL,
            embedding_model_id int8 NOT NULL REFERENCES public.embedding_models(id),
            project_id int8 NOT NULL REFERENCES public.projects(id),
            schema_version text NULL,
            kind text NOT NULL,
            language text NULL,
            namespace text NULL,
            type_name text NULL,
            member text NULL,
            source_file text NULL,
            source_hash text NULL,
            analyzed_at timestamptz NULL,
            embedding_text text NOT NULL,
            embedding_text_hash text NOT NULL,
            embedding public.vector NOT NULL,
            embedding_provider text NOT NULL,
            embedding_dimensions int4 NOT NULL,
            metadata jsonb NOT NULL,
            indexed_at timestamptz NOT NULL,
            CONSTRAINT ux_code_documents_document_id_embedding_model UNIQUE (document_id, embedding_model_id)
        );
        """;
}
