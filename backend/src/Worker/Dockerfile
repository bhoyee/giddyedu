FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY backend/ ./backend/

RUN dotnet restore backend/GiddyEdu.slnx

RUN dotnet publish backend/src/Worker/GiddyEdu.Worker.csproj 
    -c Release 
    -o /app/publish 
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "GiddyEdu.Worker.dll"]
