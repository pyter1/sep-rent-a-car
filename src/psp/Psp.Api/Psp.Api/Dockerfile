# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj first for better layer caching
COPY src/common/Common/Common.csproj src/common/Common/
COPY src/psp/Psp.Api/Psp.Api/Psp.Api.csproj src/psp/Psp.Api/Psp.Api/
RUN dotnet restore src/psp/Psp.Api/Psp.Api/Psp.Api.csproj

# copy the rest
COPY . .
RUN dotnet publish src/psp/Psp.Api/Psp.Api/Psp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 7001
ENV ASPNETCORE_URLS=http://+:7001
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Psp.Api.dll"]
