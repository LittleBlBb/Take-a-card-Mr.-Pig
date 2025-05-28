module DbConnection
open Npgsql

let connString =
    "Host=localhost;Port=5432;Database=Game;Username=postgres;Password=postgres"

let connection () = new NpgsqlConnection(connString)
