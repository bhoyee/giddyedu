FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY backend/ ./backend/

RUN dotnet restore backend/GiddyEdu.slnx

RUN dotnet publish backend/src/Api/GiddyEdu.Api.csproj 
    -c Release 
    -o /app/publish 
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GiddyEdu.Api.dll"]
