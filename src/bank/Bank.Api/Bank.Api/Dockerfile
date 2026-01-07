FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/common/Common/Common.csproj src/common/Common/
COPY src/bank/Bank.Api/Bank.Api/Bank.Api.csproj src/bank/Bank.Api/Bank.Api/
RUN dotnet restore src/bank/Bank.Api/Bank.Api/Bank.Api.csproj

COPY . .
RUN dotnet publish src/bank/Bank.Api/Bank.Api/Bank.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 7002
ENV ASPNETCORE_URLS=http://+:7002
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Bank.Api.dll"]
