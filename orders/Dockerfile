FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY src/Orders.fsproj .

RUN dotnet restore

COPY ./src .

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "Orders.dll"]
