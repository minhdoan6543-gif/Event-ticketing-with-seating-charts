FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["EventTicketing.Api/EventTicketing.Api.csproj", "EventTicketing.Api/"]
RUN dotnet restore "EventTicketing.Api/EventTicketing.Api.csproj"
COPY . .
WORKDIR "/src/EventTicketing.Api"
RUN dotnet build "EventTicketing.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EventTicketing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventTicketing.Api.dll"]
