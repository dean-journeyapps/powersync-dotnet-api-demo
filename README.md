# PowerSync .NET Backend

## Overview

This repository contains a demo .NET 9 backend API that provides authentication and data synchronization endpoints for a [PowerSync](https://www.powersync.com/) enabled application. It allows client devices to sync data with a PostgreSQL or MongoDB database.

### Endpoints

1. **GET `/api/auth/token`**
   - PowerSync uses this endpoint to retrieve a JWT access token for authentication.

2. **GET `/api/auth/keys`**
   - PowerSync uses this endpoint to validate the JWT returned from the authentication endpoint.

3. **PUT `/api/data`**
   - PowerSync uses this endpoint to sync upsert events from the client application.

4. **PATCH `/api/data`**
   - PowerSync uses this endpoint to sync update events from the client application.

5. **DELETE `/api/data`**
   - PowerSync uses this endpoint to sync delete events from the client application.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/)
- PostgreSQL, MongoDB, or MySQL database
- PowerSync credentials

## Environment Variables

The application requires the following environment variables:

```
DATABASE_URI=<your_database_connection_string>
DATABASE_TYPE=<postgresql|mongodb|mysql>
POWERSYNC_PRIVATE_KEY=<your_private_key>
POWERSYNC_PUBLIC_KEY=<your_public_key>
POWERSYNC_URL=<your_powersync_url>
PORT=3000
JWT_ISSUER=<your_jwt_issuer>
```

## Running the Application

### 1. Clone the repository
```sh
git clone https://github.com/dean-journeyapps/powersync-dotnet-api-demo.git
cd powersync-dotnet-backend
```

### 2. Build and Run with Docker

1. **Build the Docker image:**
   ```sh
   docker build -t powersync-dotnet .
   ```

2. **Run the container:**
   ```sh
   docker run -p 5000:5000 --env-file .env powersync-dotnet
   ```

This will start the app on `http://127.0.0.1:5000`.

### 3. Running Locally with .NET CLI

1. Restore dependencies:
   ```sh
   dotnet restore
   ```
2. Build the application:
   ```sh
   dotnet build
   ```
3. Run the application:
   ```sh
   dotnet run --project PowerSync.Api
   ```

## Testing the API

You can test if the API is running by opening:
```
http://127.0.0.1:5000/api/auth/token
```
You should receive a JSON response with an access token.

## Connecting the App with PowerSync

1. Download and install [ngrok](https://ngrok.com/)
2. Run the ngrok command to create an HTTPS tunnel:
   ```sh
   ngrok http 5000
   ```
3. Copy the HTTPS forwarding URL from ngrok and paste it into the [PowerSync Dashboard](https://powersync.journeyapps.com/) under the Credentials tab, using the following format:
   ```
   JWKS URI
   https://your_id.ngrok-free.app/api/auth/keys/
   ```

Ensure the URL includes the `/api/auth/keys/` path, as PowerSync uses this to validate tokens.

## Contributing

If you wish to contribute, please fork the repository and submit a pull request.

## License

This project is licensed under the MIT License.

