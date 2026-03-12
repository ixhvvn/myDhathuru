# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/MyDhathuru.Domain/MyDhathuru.Domain.csproj src/MyDhathuru.Domain/
COPY src/MyDhathuru.Application/MyDhathuru.Application.csproj src/MyDhathuru.Application/
COPY src/MyDhathuru.Infrastructure/MyDhathuru.Infrastructure.csproj src/MyDhathuru.Infrastructure/
COPY src/MyDhathuru.Api/MyDhathuru.Api.csproj src/MyDhathuru.Api/
RUN dotnet restore src/MyDhathuru.Api/MyDhathuru.Api.csproj

COPY src ./src
RUN dotnet publish src/MyDhathuru.Api/MyDhathuru.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyDhathuru.Api.dll"]
