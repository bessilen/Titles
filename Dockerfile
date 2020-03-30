FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY Titles.Api/*.csproj ./Titles.Api/
RUN dotnet restore

# copy everything else and build app
COPY Titles.Api/. ./Titles.Api/
WORKDIR /source/Titles.Api
RUN rm -r obj
RUN dotnet publish -c release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Titles.Api.dll"]