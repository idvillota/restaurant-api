FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY RestaurantManagement.sln ./
COPY src/Restaurant.Api/Restaurant.Api.csproj src/Restaurant.Api/
COPY src/Restaurant.Application/Restaurant.Application.csproj src/Restaurant.Application/
COPY src/Restaurant.Domain/Restaurant.Domain.csproj src/Restaurant.Domain/
COPY src/Restaurant.Infrastructure/Restaurant.Infrastructure.csproj src/Restaurant.Infrastructure/

RUN dotnet restore src/Restaurant.Api/Restaurant.Api.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish src/Restaurant.Api/Restaurant.Api.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-3000}
EXPOSE ${PORT:-3000}

ENTRYPOINT ["./Restaurant.Api"]
