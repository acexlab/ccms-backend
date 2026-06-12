# File: Dockerfile
# Description: Multi-stage Docker build config for ASP.NET Core Web API.
# To Implement: Uses .NET 10.0 runtime containers.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ccms-backend.csproj", "./"]
RUN dotnet restore "./ccms-backend.csproj"
COPY . .
RUN dotnet publish "ccms-backend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ccms-backend.dll"]
