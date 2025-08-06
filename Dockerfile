FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .

RUN apt-get update && apt-get install -y clang
RUN dotnet publish samples/UmbralSocket.Net.Sample/UmbralSocket.Net.Sample.csproj -c Release -r linux-x64 /p:PublishAot=true --self-contained true -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app
COPY --from=build /app/publish ./
RUN chmod +x UmbralSocket.Net.Sample
ENTRYPOINT ["./UmbralSocket.Net.Sample"]
