FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["ccms-backend.csproj", "./"]
RUN dotnet restore "./ccms-backend.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "ccms-backend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ccms-backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Environment variables
ENV ASPNETCORE_URLS=http://*:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "ccms-backend.dll"]