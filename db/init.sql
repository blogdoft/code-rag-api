-- Schema for local docker-compose use. The API itself never runs migrations; this file exists
-- purely so `docker compose up` gives you a working database out of the box.
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE public.embedding_models (
    id int8 GENERATED ALWAYS AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
    provider text NOT NULL,
    model text NOT NULL,
    dimensions int4 NOT NULL,
    "normalized" bool NOT NULL,
    "configuration" jsonb NULL,
    created_at timestamptz DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    CONSTRAINT "PK_embedding_models" PRIMARY KEY (id),
    CONSTRAINT ux_embedding_models_provider_model_dimensions UNIQUE (provider, model, dimensions)
);

CREATE TABLE public.projects (
    id int8 GENERATED ALWAYS AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
    "name" text NOT NULL,
    created_at timestamptz DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    git_url text NULL,
    git_raw_url text NULL,
    CONSTRAINT "PK_projects" PRIMARY KEY (id),
    CONSTRAINT ux_projects_name UNIQUE (name)
);

CREATE TABLE public.code_documents (
    id int8 GENERATED ALWAYS AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
    document_id text NOT NULL,
    embedding_model_id int8 NOT NULL,
    project_id int8 NOT NULL,
    schema_version text NULL,
    kind text NOT NULL,
    "language" text NULL,
    "namespace" text NULL,
    type_name text NULL,
    "member" text NULL,
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
    CONSTRAINT "PK_code_documents" PRIMARY KEY (id),
    CONSTRAINT ux_code_documents_document_id_embedding_model UNIQUE (document_id, embedding_model_id),
    CONSTRAINT "FK_code_documents_embedding_model_id_embedding_models_id" FOREIGN KEY (embedding_model_id) REFERENCES public.embedding_models(id),
    CONSTRAINT "FK_code_documents_project_id_projects_id" FOREIGN KEY (project_id) REFERENCES public.projects(id)
);
CREATE INDEX ix_code_documents_embedding_model_id ON public.code_documents USING btree (embedding_model_id);
CREATE INDEX ix_code_documents_kind ON public.code_documents USING btree (kind);
CREATE INDEX ix_code_documents_metadata_gin ON public.code_documents USING gin (metadata);
CREATE INDEX ix_code_documents_project_id ON public.code_documents USING btree (project_id);

CREATE TABLE public.code_query_feedback (
    id int8 GENERATED ALWAYS AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
    project_id int8 NOT NULL,
    question text NOT NULL,
    useful bool NOT NULL,
    similarities float8[] NOT NULL,
    reason text NULL,
    username text NOT NULL,
    created_at timestamptz DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    CONSTRAINT "PK_code_query_feedback" PRIMARY KEY (id),
    CONSTRAINT "FK_code_query_feedback_project_id_projects_id" FOREIGN KEY (project_id) REFERENCES public.projects(id)
);
CREATE INDEX ix_code_query_feedback_project_id_created_at ON public.code_query_feedback USING btree (project_id, created_at);
