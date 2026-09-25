FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CreditosApp/CreditosApp.csproj", "CreditosApp/"]
RUN dotnet restore "CreditosApp/CreditosApp.csproj"

COPY . .
WORKDIR "/src/CreditosApp"

RUN dotnet publish "CreditosApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet CreditosApp.dll --urls http://0.0.0.0:${PORT}"]