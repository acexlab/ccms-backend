# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ccms-backend.csproj ./
RUN dotnet restore ccms-backend.csproj

# Copy remaining source code and publish in Release mode
COPY . ./
RUN dotnet publish ccms-backend.csproj -c Release -o /app/publish

# Stage 2: Runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose HTTP ports
EXPOSE 80
EXPOSE 5000

# Setup runtime environment variables
ENV ASPNETCORE_URLS=http://+:5000;http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ccms-backend.dll"]
