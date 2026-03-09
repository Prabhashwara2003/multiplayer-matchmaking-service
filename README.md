# Multiplayer Matchmaking Service

A robust, ASP.NET Core 9 Web API service for multiplayer game matchmaking. It handles grouping players into parties, queuing them by region, and automatically creating matches based on dynamic MMR (Matchmaking Rating) tolerances.

## Features
- **Party Queueing**: Join as a solo player or a party of 2.
- **Dynamic MMR Tolerance**: The longer players wait, the wider the acceptable MMR difference becomes.
- **Background Matchmaking**: A dedicated background worker efficiently processes queues and forms matches.
- **SQLite Database**: Lightweight, persistent storage for player profiles and MMR tracking using Entity Framework Core.
- **Docker Support**: Fully containerized for easy deployment without host-side .NET dependencies.

## Prerequisites
- **Docker** (Recommended)
- OR **.NET 9 SDK** (If running locally on the host)

## Getting Started

### Using Docker (Easiest)
You can run the service entirely within Docker. This requires no .NET installation on your machine.

1. **Build the image**:
   ```bash
   docker build -t matchmaking-service .
   ```
2. **Run the container**:
   ```bash
   docker run -d --name matchmaking-app -p 5154:8080 matchmaking-service
   ```
The API will be available at `http://localhost:5154`.

### Using .NET CLI
If you have the .NET 9 SDK installed:

1. **Run the application**:
   ```bash
   dotnet run
   ```
The API will be available at `http://localhost:5154`.

## Testing the API

You can test the full lifecycle of a match using `curl` in your Command Prompt. 
*Note: The system requires 4 players (two parties of 2) in the same region to create a match.*

**1. Add Players to Queue:**
```cmd
curl -X POST "http://localhost:5154/queue/join" -H "Content-Type: application/json" -d "{ \"PlayerIds\": [\"player_a\", \"player_b\"], \"Region\": \"US\", \"PlayerMmrs\": { \"player_a\": 1500, \"player_b\": 1500 } }"

curl -X POST "http://localhost:5154/queue/join" -H "Content-Type: application/json" -d "{ \"PlayerIds\": [\"player_c\", \"player_d\"], \"Region\": \"US\", \"PlayerMmrs\": { \"player_c\": 1510, \"player_d\": 1490 } }"
```

**2. Check Match Status:**
(Returns a `matchId`)
```cmd
curl -X GET "http://localhost:5154/match/player_a"
```

**3. Accept the Match:**
All 4 players must accept using the returned `matchId`:
```cmd
curl -X POST "http://localhost:5154/match/<MATCH_ID>/accept/player_a"
curl -X POST "http://localhost:5154/match/<MATCH_ID>/accept/player_b"
curl -X POST "http://localhost:5154/match/<MATCH_ID>/accept/player_c"
curl -X POST "http://localhost:5154/match/<MATCH_ID>/accept/player_d"
```

**4. Report Results:**
(1 for Team 1, 2 for Team 2)
```cmd
curl -X POST "http://localhost:5154/match/<MATCH_ID>/result" -H "Content-Type: application/json" -d "{ \"WinningTeam\": 1 }"
```

**5. Verify MMR Changes:**
```cmd
curl -X GET "http://localhost:5154/player/player_a"
```
