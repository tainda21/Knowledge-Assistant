FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY WebAPI.csproj .
RUN dotnet restore WebAPI.csproj

COPY . .
RUN dotnet publish WebAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "WebAPI.dll"]
