FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CodeRag.sln ./
COPY src/CodeRag.Api/CodeRag.Api.csproj src/CodeRag.Api/
COPY src/CodeRag.Application/CodeRag.Application.csproj src/CodeRag.Application/
COPY src/CodeRag.Embeddings.Abstraction/CodeRag.Embeddings.Abstraction.csproj src/CodeRag.Embeddings.Abstraction/
COPY src/CodeRag.Embeddings.Local/CodeRag.Embeddings.Local.csproj src/CodeRag.Embeddings.Local/
COPY src/CodeRag.Embeddings.Ollama/CodeRag.Embeddings.Ollama.csproj src/CodeRag.Embeddings.Ollama/
COPY src/CodeRag.Embeddings.OpenAI/CodeRag.Embeddings.OpenAI.csproj src/CodeRag.Embeddings.OpenAI/
COPY src/CodeRag.Infrastructure.Database/CodeRag.Infrastructure.Database.csproj src/CodeRag.Infrastructure.Database/
RUN dotnet restore src/CodeRag.Api/CodeRag.Api.csproj

COPY src/ src/
RUN dotnet publish src/CodeRag.Api/CodeRag.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN useradd --create-home appuser
USER appuser

COPY --from=build --chown=appuser:appuser /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CodeRag.Api.dll"]
