# Stage 1: build React UI
FROM node:22-alpine AS ui-build
WORKDIR /app/ui
COPY fx-sandbox-ui/package*.json ./
RUN npm ci
COPY fx-sandbox-ui/ ./
RUN npm run build

# Stage 2: build .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
WORKDIR /app
COPY FxSandbox.sln ./
COPY FxSandbox.Api/FxSandbox.Api.csproj FxSandbox.Api/
COPY FxSandbox.Tests/FxSandbox.Tests.csproj FxSandbox.Tests/
RUN dotnet restore
COPY FxSandbox.Api/ FxSandbox.Api/
RUN dotnet publish FxSandbox.Api -c Release -o /publish

# Stage 3: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=api-build /publish ./
# Embed React build into wwwroot so the API can serve it
COPY --from=ui-build /app/ui/dist ./wwwroot
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FxSandbox.Api.dll"]
