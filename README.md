# AsyncSQL

AsyncSQL is the missing library in dotnet world for executing raw SQL queries asynchronously for game server development. It's implemented not with async/await but with multi-threading and callbacks, since typical game servers should have full control on threading.
