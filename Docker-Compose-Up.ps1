$buildDate = (Get-Date).ToUniversalTime().ToString("o")

docker compose build --build-arg BUILD_DATE=$buildDate
docker compose create mcpserver
docker compose start mcpserver
