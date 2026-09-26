# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source
ARG SERVICE=Costumer
ENV SERVICE_NAME=${SERVICE}
WORKDIR /src
ARG SERVICE_DIR=costumer-api
COPY ${SERVICE_DIR}/ ./
RUN dotnet restore "${SERVICE_NAME}.slnx"

FROM source AS publish
RUN dotnet publish "${SERVICE_NAME}.Api/${SERVICE_NAME}.Api.csproj" \
    -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM source AS migrations
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.12
ENTRYPOINT ["sh", "-c", "exec /tools/dotnet-ef database update --project \"${SERVICE_NAME}.Infrastructure\" --startup-project \"${SERVICE_NAME}.Api\""]

FROM source AS tests
ENTRYPOINT ["sh", "-c", "exec dotnet test \"${SERVICE_NAME}.slnx\" -c Release --no-restore -m:1"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG SERVICE=Costumer
ENV SERVICE_NAME=${SERVICE}
ENV ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=publish /app/publish ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"${SERVICE_NAME}.Api.dll\""]
