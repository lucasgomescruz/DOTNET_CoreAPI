FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.sln . 
COPY Directory.Build.props .
COPY nuget.config .
COPY source/WebApi/Project.WebApi.csproj ./source/WebApi/
COPY source/Application/Project.Application.csproj ./source/Application/
COPY source/Infrastructure/Project.Infrastructure.csproj ./source/Infrastructure/
COPY source/Domain/Project.Domain.csproj ./source/Domain/
COPY tests/Project.Tests.Unit/Project.Tests.Unit.csproj ./tests/Project.Tests.Unit/
RUN dotnet restore

RUN dotnet tool install --global dotnet-ef

ENV PATH="${PATH}:/root/.dotnet/tools"

COPY . .

WORKDIR /app/source/WebApi
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /out .

EXPOSE 5000

ENTRYPOINT ["dotnet", "Project.WebApi.dll"]
