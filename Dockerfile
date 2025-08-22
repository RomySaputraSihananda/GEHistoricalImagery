FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.sln ./

COPY ./src ./src
COPY ./test ./test

RUN dotnet restore

COPY . .

RUN dotnet publish src/GEHistoricalImagery/GEHistoricalImagery.csproj \
    -c Release \
    /p:DefineConstants=LINUX \
    --runtime linux-x64 \
    -o /out/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /out

COPY --from=build /out/publish ./

ENTRYPOINT ["dotnet", "GEHistoricalImagery.dll"]