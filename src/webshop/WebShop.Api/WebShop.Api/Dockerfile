FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/common/Common/Common.csproj src/common/Common/
COPY src/webshop/WebShop.Api/WebShop.Api/WebShop.Api.csproj src/webshop/WebShop.Api/WebShop.Api/
RUN dotnet restore src/webshop/WebShop.Api/WebShop.Api/WebShop.Api.csproj

COPY . .
RUN dotnet publish src/webshop/WebShop.Api/WebShop.Api/WebShop.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 7003
ENV ASPNETCORE_URLS=http://+:7003
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebShop.Api.dll"]
